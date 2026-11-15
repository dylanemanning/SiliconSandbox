using System;
using System.Diagnostics;
using System.IO;
using System.Text;

public static class WorldFileDialog
{
    public static string OpenWorldFile()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ShowWindowsDialog(false, "Select a world JSON file", null);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return ShowMacDialog(
            "try\n" +
            "set selectedFile to choose file with prompt \"Select a world JSON file\"\n" +
            "return POSIX path of selectedFile\n" +
            "on error number -128\n" +
            "return \"\"\n" +
            "end try"
        );
#else
        throw new PlatformNotSupportedException("World file dialogs are supported on Windows and macOS.");
#endif
    }

    public static string SaveWorldFile(string defaultFileName)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ShowWindowsDialog(true, "Save world JSON", defaultFileName);
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        string escapedFileName = EscapeAppleScriptString(defaultFileName);
        return ShowMacDialog(
            "try\n" +
            "set selectedFile to choose file name with prompt \"Save world JSON\" default name \"" +
            escapedFileName + "\"\n" +
            "return POSIX path of selectedFile\n" +
            "on error number -128\n" +
            "return \"\"\n" +
            "end try"
        );
#else
        throw new PlatformNotSupportedException("World file dialogs are supported on Windows and macOS.");
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static string ShowWindowsDialog(bool save, string title, string defaultFileName)
    {
        string dialogType = save ? "SaveFileDialog" : "OpenFileDialog";
        string fileName = save ? EscapePowerShellString(defaultFileName ?? string.Empty) : string.Empty;
        string script =
            "$ErrorActionPreference = 'Stop'\n" +
            "try {\n" +
            "  Add-Type -AssemblyName System.Windows.Forms\n" +
            $"  $dialog = New-Object System.Windows.Forms.{dialogType}\n" +
            "  $dialog.Title = '" + EscapePowerShellString(title) + "'\n" +
            "  $dialog.Filter = 'World JSON (*.json)|*.json|All files (*.*)|*.*'\n" +
            "  $dialog.DefaultExt = 'json'\n" +
            "  $dialog.AddExtension = $true\n" +
            (save ? "  $dialog.FileName = '" + fileName + "'\n" : string.Empty) +
            "  if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {\n" +
            "    [Console]::Out.WriteLine([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($dialog.FileName)))\n" +
            "  }\n" +
            "  $dialog.Dispose()\n" +
            "} catch {\n" +
            "  [Console]::Error.WriteLine($_.Exception.Message)\n" +
            "  exit 1\n" +
            "}";

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string powershellPath = Path.Combine(
            windowsDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"
        );
        if (!File.Exists(powershellPath))
        {
            throw new FileNotFoundException("Windows PowerShell is required to show the world file dialog.", powershellPath);
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            Arguments = "-NoProfile -NonInteractive -STA -WindowStyle Hidden -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            if (process == null) throw new InvalidOperationException("Could not start the Windows file dialog.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not open the Windows file dialog: " + error.Trim());
            }

            string encodedPath = output.Trim();
            return string.IsNullOrEmpty(encodedPath)
                ? string.Empty
                : Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));
        }
    }

    private static string EscapePowerShellString(string value)
    {
        return value.Replace("'", "''");
    }
#endif

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private static string ShowMacDialog(string script)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/osascript",
            Arguments = "-e " + QuoteArgument(script),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            if (process == null) throw new InvalidOperationException("Could not start the macOS file dialog.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("Could not open the macOS file dialog: " + error.Trim());
            }

            return output.Trim();
        }
    }

    private static string EscapeAppleScriptString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
#endif
}
