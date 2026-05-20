namespace PassKey.Core.Services;

/// <summary>
/// Queries the Have I Been Pwned "Pwned Passwords" API
/// (<see href="https://haveibeenpwned.com/API/v3#PwnedPasswords"/>) using the
/// <b>k-anonymity</b> protocol: only the first 5 hex characters of the SHA-1
/// hash of the password ever leave the device, so HIBP can never identify the
/// password being checked.
/// </summary>
/// <remarks>
/// <para>The free <c>api.pwnedpasswords.com</c> endpoint backs every check —
/// no API key, no auth, no rate-limit beyond a courtesy cap that the
/// <see cref="WatchtowerScanService"/> respects when scanning many entries
/// in a row.</para>
/// <para>Privacy: this service issues network requests. PassKey is offline-first
/// by default — the caller is responsible for honouring the user's opt-in
/// preference (see <c>AppSettings.HibpEnabled</c>) and short-circuiting before
/// reaching this service when the user has not consented.</para>
/// </remarks>
public interface IHibpService
{
    /// <summary>
    /// Checks <paramref name="password"/> against the HIBP Pwned Passwords list.
    /// </summary>
    /// <param name="password">The cleartext password to check. Never leaves the device.</param>
    /// <param name="cancellationToken">Optional cancellation token (e.g. timeout from a scan).</param>
    /// <returns>
    /// The number of distinct breaches in which the password has been observed.
    /// <c>0</c> means "not seen in any known breach" (still possible the password
    /// is weak — HIBP doesn't grade strength, only known compromise).
    /// </returns>
    /// <exception cref="HttpRequestException">Thrown on network failure / non-2xx response.</exception>
    /// <exception cref="TaskCanceledException">Thrown on timeout / explicit cancellation.</exception>
    Task<int> CheckPasswordAsync(string password, CancellationToken cancellationToken = default);
}
