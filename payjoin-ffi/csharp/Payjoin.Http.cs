using uniffi.payjoin;

namespace Payjoin.Http
{
    internal static class OhttpKeysClient
    {
        /// <summary>
        /// Fetches the OHTTP keys from the specified payjoin directory via proxy.
        /// </summary>
        /// <param name="ohttpRelayUrl">
        /// The HTTP CONNECT method proxy to request the OHTTP keys from a payjoin directory.
        /// Proxying requests for OHTTP keys ensures a client IP address is never revealed to
        /// the payjoin directory.
        /// </param>
        /// <param name="directoryUrl">
        /// The payjoin directory from which to fetch the OHTTP keys. This directory stores
        /// and forwards payjoin client payloads.
        /// </param>
        /// <param name="certificate">The DER-encoded certificate to use for local HTTPS connections.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>The decoded <see cref="OhttpKeys"/> from the payjoin directory.</returns>
        internal static async Task<OhttpKeys> GetOhttpKeysAsync(System.Uri ohttpRelayUrl, System.Uri directoryUrl, byte[] certificate, CancellationToken cancellationToken = default)
        {
            var keysUrl = new System.Uri(directoryUrl, "/.well-known/ohttp-gateway");

            using var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(ohttpRelayUrl),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = (_, serverCert, _, _) => serverCert != null && serverCert.GetRawCertData().SequenceEqual(certificate)
            };

            using var client = new HttpClient(handler);
            using var request = new HttpRequestMessage(HttpMethod.Get, keysUrl);
            request.Headers.Accept.ParseAdd("application/ohttp-keys");

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var ohttpKeysBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return OhttpKeys.Decode(ohttpKeysBytes);
        }
    }
}
