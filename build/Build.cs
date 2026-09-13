using Fallout.Common;
using Fallout.Solutions;
using Automation.Fallout.Components;
using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.DefaultBuilds;
using Automation.Fallout.Components.Parameters;

/// <summary>
/// Build configuration for VelopackBuild
/// </summary>

public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IScanForSecrets, IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest, IUpdateChangelog, IVelopack, ITagRelease, IAnnounceRelease, ICreateGitHubRelease
{

    public static int Main() => Execute<Build>(
        y => ((IVelopack)y).ReleaseVelopack);

    string IHasVelopack.VelopackProjectName => "Greenlight.FalloutClient";
    string IHasVelopack.VelopackIconPath => @"Greenlight.FalloutClient\Assets\MeddlingIdiot.ico";
    int IHasTests.MinCoverageThreshold => 20;
    bool IHasTests.BreakBuildOnSecretLeaks => false;

    // Automation.Fallout.Components defaults these to AFTR's staftrinstallers, which this project has
    // no access to. Every MeddlingIdiot installer lives in meddlingidiotinstallers; the Nuke-era
    // library defaulted there, and the migration to Fallout silently moved the destination.
    // Without these the SAS token is sent to the wrong account and Azure rejects it with
    // AuthenticationFailed (403) — which reads like a missing token but is a mismatched one
    string IHasVelopack.AzureBlobAccount => "meddlingidiotinstallers";
    string IHasVelopack.AzureBlobEndpoint => "https://meddlingidiotinstallers.blob.core.windows.net";
}
