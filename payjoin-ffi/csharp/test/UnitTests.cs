using System;
using System.Collections.Generic;
using Xunit;
using uniffi.payjoin;
using Uri = uniffi.payjoin.Uri;

namespace Payjoin.CSharp.Tests
{
    public class UnitTests
    {
        private const string OriginalPsbt =
            "cHNidP8BAHMCAAAAAY8nutGgJdyYGXWiBEb45Hoe9lWGbkxh/6bNiOJdCDuDAAAAAAD+////AtyVuAUAAAAAF6kUHehJ8GnSdBUOOv6ujXLrWmsJRDCHgIQeAAAAAAAXqRR3QJbbz0hnQ8IvQ0fptGn+votneofTAAAAAAEBIKgb1wUAAAAAF6kU3k4ekGHKWRNbA1rV5tR5kEVDVNCHAQcXFgAUx4pFclNVgo1WWAdN1SYNX8tphTABCGsCRzBEAiB8Q+A6dep+Rz92vhy26lT0AjZn4PRLi8Bf9qoB/CMk0wIgP/Rj2PWZ3gEjUkTlhDRNAQ0gXwTO7t9n+V14pZ6oljUBIQMVmsAaoNWHVMS02LfTSe0e388LNitPa1UQZyOihY+FFgABABYAFEb2Giu6c4KO5YW0pfw3lGp9jMUUAAA=";
        private const string OhttpKeysHex =
            "01001604ba48c49c3d4a92a3ad00ecc63a024da10ced02180c73ec12d8a7ad2cc91bb483824fe2bee8d28bfe2eb2fc6453bc4d31cd851e8a6540e86c5382af588d370957000400010003";

        private sealed class InMemorySenderPersister : JsonSenderSessionPersister
        {
            private readonly List<string> _events = new();

            public void Save(string @event) => _events.Add(@event);

            public string[] Load() => _events.ToArray();

            public void Close()
            {
            }
        }

        private sealed class InMemoryReceiverPersister : JsonReceiverSessionPersister
        {
            private readonly List<string> _events = new();

            public void Save(string @event) => _events.Add(@event);

            public string[] Load() => _events.ToArray();

            public void Close()
            {
            }
        }

        [Fact]
        public void UriParseAllowsUrlEncodedPayjoinParameter()
        {
            var uri = "bitcoin:12c6DSiU4Rq3P4ZxziKxzrL5LmMBrzjrJX?amount=1&pj=https://example.com?ciao";
            using var parsed = Uri.Parse(uri);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void UriParseAllowsMissingAmount()
        {
            var uri = "bitcoin:12c6DSiU4Rq3P4ZxziKxzrL5LmMBrzjrJX?pj=https://testnet.demo.btcpayserver.org/BTC/pj";
            using var parsed = Uri.Parse(uri);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void UriParseAcceptsValidPayjoinUris()
        {
            var https = "https://example.com";
            var onion = "http://vjdpwgybvubne5hda6v4c5iaeeevhge6jvo3w2cl6eocbwwvwxp7b7qd.onion";

            var addresses = new[]
            {
                "bitcoin:12c6DSiU4Rq3P4ZxziKxzrL5LmMBrzjrJX",
                "BITCOIN:TB1Q6D3A2W975YNY0ASUVD9A67NER4NKS58FF0Q8G4",
                "bitcoin:tb1q6d3a2w975yny0asuvd9a67ner4nks58ff0q8g4",
            };

            foreach (var address in addresses)
            {
                foreach (var pj in new[] { https, onion })
                {
                    using var parsed = Uri.Parse($"{address}?amount=1&pj={pj}");
                    Assert.NotNull(parsed);
                }
            }
        }

        [Fact]
        public void ReceiverPersistenceReplaysInitializedState()
        {
            var persister = new InMemoryReceiverPersister();
            using var ohttpKeys = OhttpKeys.Decode(Convert.FromHexString(OhttpKeysHex));
            using var builder = new ReceiverBuilder(
                "tb1q6d3a2w975yny0asuvd9a67ner4nks58ff0q8g4",
                "https://example.com",
                ohttpKeys);
            using var transition = builder.Build();
            using (var receiver = transition.Save(persister))
            {
                Assert.NotNull(receiver);
            }
            using var replay = PayjoinMethods.ReplayReceiverEventLog(persister);
            using var state = replay.State();
            Assert.IsType<ReceiveSession.Initialized>(state);
        }

        [Fact]
        public void SenderPersistenceCreatesSession()
        {
            var receiverPersister = new InMemoryReceiverPersister();
            using var ohttpKeys = OhttpKeys.Decode(Convert.FromHexString(OhttpKeysHex));
            using var receiverBuilder = new ReceiverBuilder(
                "2MuyMrZHkbHbfjudmKUy45dU4P17pjG2szK",
                "https://example.com",
                ohttpKeys);
            using var receiverTransition = receiverBuilder.Build();
            using var receiver = receiverTransition.Save(receiverPersister);
            using var pjUri = receiver.PjUri();

            var senderPersister = new InMemorySenderPersister();
            using var senderBuilder = new SenderBuilder(OriginalPsbt, pjUri);
            using var senderTransition = senderBuilder.BuildRecommended(1000);
            using var sender = senderTransition.Save(senderPersister);
            Assert.NotNull(sender);
        }

        [Fact]
        public void ReceiverBuilderRejectsBadAddress()
        {
            using var ohttpKeys = OhttpKeys.Decode(Convert.FromHexString(OhttpKeysHex));
            var ex = Assert.Throws<ReceiverBuilderException.InvalidAddress>(() =>
                new ReceiverBuilder("not-an-address", "https://example.com", ohttpKeys));
            ex.Dispose();
        }

        [Fact]
        public void InputPairRejectsInvalidOutpoint()
        {
            var txin = new PlainTxIn(
                new PlainOutPoint("deadbeef", 0),
                Array.Empty<byte>(),
                0,
                Array.Empty<byte[]>());
            var psbtIn = new PlainPsbtInput(null, null, null);
            var ex = Assert.Throws<InputPairException.InvalidOutPoint>(() =>
            {
                using var _ = new InputPair(txin, psbtIn, null);
            });
            ex.Dispose();
        }
    }
}
