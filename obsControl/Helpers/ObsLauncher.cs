using System;
using System.Diagnostics;
using System.IO;

namespace obsControl;

internal static class ObsLauncher
{
    private static readonly string[] CandidatePaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "obs-studio", "bin", "64bit", "obs64.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio", "bin", "64bit", "obs64.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "obs-studio", "bin", "64bit", "obs64.exe"),
    ];

    public static bool TryLaunch(out string? error)
    {
        error = null;

        foreach (var path in CandidatePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true,
                });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        error = "obs64.exe not found";
        return false;
    }
}
