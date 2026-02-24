using System;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium.DevTools;

namespace Selenium.HarCapture.Capture.Internal.Cdp;

/// <summary>
/// Creates the appropriate CDP Network adapter by auto-discovering available CDP versions
/// via assembly scanning. Tries newest version first.
/// </summary>
internal static class CdpAdapterFactory
{
    /// <summary>
    /// Creates an <see cref="ICdpNetworkAdapter"/> for the given DevTools session.
    /// Scans the Selenium assembly for all V{N}.DevToolsSessionDomains types
    /// and tries them from newest to oldest.
    /// </summary>
    /// <param name="session">An active DevTools session.</param>
    /// <returns>A reflection-based adapter that implements <see cref="ICdpNetworkAdapter"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no compatible CDP version is found in the assembly.
    /// </exception>
    internal static ICdpNetworkAdapter Create(DevToolsSession session)
    {
        var assembly = typeof(DevToolsSession).Assembly;

        var versionTypes = assembly.GetTypes()
            .Select(t => (Type: t, Match: Regex.Match(t.FullName ?? "", @"\.V(\d+)\.DevToolsSessionDomains$")))
            .Where(x => x.Match.Success)
            .Select(x => (x.Type, Version: int.Parse(x.Match.Groups[1].Value)))
            .OrderByDescending(x => x.Version)
            .ToList();

        foreach (var (domainsType, _) in versionTypes)
        {
            try
            {
                return new ReflectiveCdpNetworkAdapter(session, domainsType);
            }
            catch (InvalidOperationException)
            {
                // Version mismatch with the browser, try next
            }
        }

        var tried = string.Join(", ", versionTypes.Select(x => $"V{x.Version}"));
        throw new InvalidOperationException(
            $"No compatible CDP version found in the assembly. Tried: {tried}. " +
            "Ensure your Chrome/Edge version is compatible with the installed Selenium.WebDriver package. " +
            "Use CaptureOptions.ForceSeleniumNetworkApi = true as a workaround.");
    }
}
