using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Management;
using Microsoft.Win32;

class Program
{
    // ImgDrive installation path
    private static string _ImgDrivePath = null;
    private static readonly string[] _supportedImageFormats = { ".iso", ".bin", ".cue", ".img", ".mdf", ".nrg", ".cdi", ".dmg" };
    private static readonly string _ImgDriveExecutables = "imgdrive.exe";

    // File to store the mounted image path
    private static readonly string MountInfoFile = Path.Combine(Path.GetTempPath(), "IsoMounter.mount");
    // Log file path
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IsoMounter.log");

    // Constants for mounting disk images
    private const int DDD_RAW_TARGET_PATH = 0x1;
    private const int DDD_REMOVE_DEFINITION = 0x2;
    private const int DONT_RESOLVE_DLL_REFERENCES = 0x00000001;

    // Importing the necessary Windows functions
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DefineDosDevice(int dwFlags, string lpDeviceName, string lpTargetPath);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetDriveType(string lpRootPathName);
    
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetVolumeNameForVolumeMountPoint(string lpszVolumeMountPoint, 
                                                             [Out] StringBuilder lpszVolumeName, 
                                                             uint cchBufferLength);

     // Saves the mounted image path to a temporary file
    private static void SaveMountedImage(string imagePath)
    {
        File.WriteAllText(MountInfoFile, imagePath, Encoding.UTF8);
    }

      // Gets the path of the currently mounted image
    private static string GetMountedImagePath()
    {
        if (File.Exists(MountInfoFile))
        {
            string path = File.ReadAllText(MountInfoFile, Encoding.UTF8).Trim();
            if (File.Exists(path))
            {
                return path;
            }
        }
        return null;
    }

    // Removes the mount information file
    private static void ClearMountedImage()
    {
        if (File.Exists(MountInfoFile))
        {
            File.Delete(MountInfoFile);
        }
    }

    // Logs a message to both file and console
    private static void LogMessage(string message, bool isError = false)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string logMessage = $"[{timestamp}] {(isError ? "ERROR" : "INFO")} - {message}";
            
            // Write to console
            Console.WriteLine(logMessage);
            
            // Write to log file
            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to the log file: {ex.Message}");
        }
    }
    
    // Unmounts the previously mounted image
    private static int UnmountImage()
    {
        string mountedImage = GetMountedImagePath();
        if (mountedImage == null)
        {
            LogMessage("No mounted image found");
            return 1;
        }

        LogMessage($"Starting to unmount image: {mountedImage}");
        Console.WriteLine($"Unmounting image: {mountedImage}");
        
        try
        {
            // Check if ImgDrive is available
            bool hasImgDrive = IsImgDriveInstalled();
            bool success = false;
            
            if (hasImgDrive)
            {
                // Use ImgDrive for all types of images
                LogMessage("Using ImgDrive for unmounting");
                success = UnmountWithImgDrive(mountedImage);
            }
            else
            {
                // Check if it's an ISO for native Windows unmounting
                string extension = Path.GetExtension(mountedImage)?.ToLowerInvariant();
                bool isIso = extension == ".iso";
                
                if (isIso)
                {
                    // Use native Windows unmounting only for ISOs
                    LogMessage("Using native Windows unmount for ISO");
                    success = UnmountWithWindowsNative(mountedImage);
                }
                else
                {
                    LogMessage("No unmount method available for this image format", true);
                    return 1;
                }
            }

            if (success)
            {
                ClearMountedImage();
                LogMessage("Unmount successful");
                return 0;
            }
            
            LogMessage("Failed to unmount with all methods", true);
            return 1;
        }
        catch (Exception ex)
        {
            LogMessage($"Error while unmounting: {ex.Message}", true);
            return 1;
        }
    }

    // Unmounts an image using the native Windows method (for ISOs)
    private static bool UnmountWithWindowsNative(string imagePath)
    {
        try
        {
            LogMessage("Trying native Windows unmount...");
            
            string psCommand = $"Dismount-DiskImage -ImagePath '{imagePath.Replace("'", "''")}'";
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (!string.IsNullOrEmpty(output)) LogMessage($"Sortie native: {output}");
                if (!string.IsNullOrEmpty(error)) LogMessage($"Erreur native: {error}", true);

                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error during native unmount: {ex.Message}", true);
            return false;
        }
    }

    // Unmount an image with ImgDrive.
    private static bool UnmountWithImgDrive(string imagePath)
    {
        try
        {
            if (string.IsNullOrEmpty(_ImgDrivePath))
            {
                LogMessage("ImgDrive is not installed");
                return false;
            }

            string ImgDriveDir = Path.GetDirectoryName(_ImgDrivePath);
            string ImgDrivePath = Path.Combine(ImgDriveDir, "imgdrive.exe");
            
            if (!File.Exists(ImgDrivePath))
            {
                LogMessage("No ImgDrive executable found for unount the file.", true);
                return false;
            }

            // Use the exact syntax that works on the command line.
            string arguments = $"-u \"{imagePath}\"";
            LogMessage($"Command execution: {Path.GetFileName(ImgDrivePath)} {arguments}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = ImgDrivePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ImgDriveDir
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        outputBuilder.AppendLine(e.Data);
                        LogMessage($"Sortie: {e.Data}");
                    }
                };
                
                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        errorBuilder.AppendLine(e.Data);
                        LogMessage($"Erreur: {e.Data}", true);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                bool success = process.WaitForExit(10000);
                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();
                
                if (!success || process.ExitCode != 0)
                {
                    LogMessage($"Error unmounting the file. Exit code: {process.ExitCode}", true);
                    if (!string.IsNullOrEmpty(output)) LogMessage($"Sortie complète: {output}", true);
                    if (!string.IsNullOrEmpty(error)) LogMessage($"Erreur complète: {error}", true);
                    return false;
                }
                
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error unmounting ImgDrive: {ex.Message}", true);
            return false;
        }
    }

    // Checks if an image is already mounted
    private static bool IsImageMounted(string imagePath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -NonInteractive -Command \"& {{ (Get-DiskImage -ImagePath '{imagePath.Replace("'", "''")}').Attached }}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return output.Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // Detects if ImgDrive is installed and returns the installation path.
    private static bool IsImgDriveInstalled()
    {
        try
        {
            // Check in the Windows registry
            string[] registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ImgDrive",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\ImgDrive"
            };

            foreach (var registryPath in registryPaths)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        string installPath = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installPath))
                        {
                            string fullPath = Path.Combine(installPath, _ImgDriveExecutables);
                            if (File.Exists(fullPath))
                            {
                                _ImgDrivePath = fullPath;
                                LogMessage($"ImgDrive found: {_ImgDrivePath}");
                                return true;
                            }
                        }
                    }
                }
            }

            // Check in the Program Files folders
            string[] programFilesPaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ImgDrive"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ImgDrive")
            };

            foreach (var path in programFilesPaths.Distinct())
            {
                if (Directory.Exists(path))
                {
                    string fullPath = Path.Combine(path, _ImgDriveExecutables);
                    if (File.Exists(fullPath))
                    {
                        _ImgDrivePath = fullPath;
                        LogMessage($"ImgDrive found in Program Files: {_ImgDrivePath}");
                        return true;
                    }
                }
            }

            // Check in the system PATH
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var path in pathEnv.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    string fullPath = Path.Combine(path, _ImgDriveExecutables);
                    if (File.Exists(fullPath))
                    {
                        _ImgDrivePath = fullPath;
                        LogMessage($"ImgDrive found in the PATH: {_ImgDrivePath}");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error detecting ImgDrive: {ex.Message}", true);
        }

        LogMessage("ImgDrive not found on the system", true);
        return false;
    }

    // Mount an image with ImgDrive
    private static bool MountWithImgDrive(string imagePath)
    {
        try
        {
            string ImgDriveDir = Path.GetDirectoryName(_ImgDrivePath);

            // Check if imgdrive.exe exists
            string ImgDriveExePath = Path.Combine(ImgDriveDir, "imgdrive.exe");
            if (!File.Exists(ImgDriveExePath))
            {
                LogMessage("Executable not found in the ImgDrive folder.", true);
                return false;
            }

            // Mount the image and use the /wait option
            var startInfo = new ProcessStartInfo
            {
                FileName = ImgDriveExePath,
                Arguments = $@"-m ""{imagePath}""",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = ImgDriveDir
            };

            LogMessage($"Execution of: {Path.GetFileName(ImgDriveExePath)} {startInfo.Arguments}");

            using (var process = new Process { StartInfo = startInfo })
            {
                // Read the output asynchronously to avoid deadlocks
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        outputBuilder.AppendLine(e.Data);
                        LogMessage($"Sortie: {e.Data}");
                    }
                };
                
                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        errorBuilder.AppendLine(e.Data);
                        LogMessage($"Erreur: {e.Data}", true);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                bool success = process.WaitForExit(30000); // Wait a maximum of 30 seconds

                if (!success)
                {
                    LogMessage("The assembly process took too long.", true);
                    return false;
                }
                
                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();
                
                if (process.ExitCode != 0)
                {
                    LogMessage($"Error during mounting with ImgDrive. Exit code: {process.ExitCode}", true);
                    if (!string.IsNullOrEmpty(output)) LogMessage($"Sortie complète: {output}", true);
                    if (!string.IsNullOrEmpty(error)) LogMessage($"Erreur complète: {error}", true);
                    return false;
                }
                
                LogMessage("Image successfully mounted via ImgDrive");
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error during mounting with ImgDrive: {ex.Message}", true);
            return false;
        }
    }

    // Mount an ISO image using PowerShell
    private static bool MountIso(string isoPath)
    {
        try
        {
            // Check if file exists
            if (!File.Exists(isoPath))
            {
                LogMessage($"ISO file does not exist: {isoPath}", true);
                return false;
            }

            // Check if image is already mounted
            if (IsImageMounted(isoPath))
            {
                LogMessage("Image is already mounted");
                return true;
            }

            // Prepare PowerShell command
            string psCommand = $"$null = Mount-DiskImage -ImagePath '{isoPath.Replace("'", "''")}' -PassThru -ErrorAction Stop";
            
            LogMessage($"Starting mount process...");
            
            var stopwatch = Stopwatch.StartNew();
            
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -NoProfile -NonInteractive -Command \"& {{ {psCommand} }}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                
                // Wait a bit to allow mounting to start
                if (!process.WaitForExit(5000)) // 5 seconds maximum
                {
                    LogMessage("Mounting is taking longer than expected");
                }
                
                process.WaitForExit();
                stopwatch.Stop();
                
                LogMessage($"Mounting time: {stopwatch.ElapsedMilliseconds}ms");

                if (process.ExitCode != 0)
                {
                    LogMessage($"Mounting failed with exit code: {process.ExitCode}", true);
                    return false;
                }

                // Verify the mount was successful
                int retries = 10; // Number of verification attempts
                while (retries-- > 0)
                {
                    if (IsImageMounted(isoPath))
                    {
                        LogMessage("Image mounted successfully");
                        return true;
                    }
                    Thread.Sleep(100); // Wait 100ms between checks
                }

                LogMessage("Mounting seems successful but not yet visible", true);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error mounting ISO: {ex.Message}", true);
            return false;
        }
    }

    // Initializes the logging system
    private static void InitializeLog()
    {
        try
        {
            // Create log file or overwrite if it exists
            File.WriteAllText(LogFilePath, "", Encoding.UTF8);
            LogMessage("Application starting");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing log: {ex.Message}");
        }
    }

    // Main application entry point
    static int Main(string[] args)
    {
        InitializeLog();
        
        // Arguments validation
        if (args.Length == 0)
        {
            string errorMessage = "No arguments provided. Usage: Mount: IsoMounter.exe \"path/to/rom\" or Unmount: IsoMounter.exe --unmount";
            LogMessage($"{errorMessage}", true);
            Console.WriteLine($"Error: {errorMessage}");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Mount: IsoMounter.exe \"path/to/rom\"");
            Console.WriteLine("  Unmount: IsoMounter.exe --unmount");
            return 1;
        }

        // Unmount mode
        if (args[0].Equals("--unmount", StringComparison.OrdinalIgnoreCase))
        {
            return UnmountImage();
        }

        // Default to non-interactive mode for RetroBat usage
        bool interactive = args.Any(a => a == "--interactive");
        
        // Filter arguments to keep only the game path and relevant options
        string[] filteredArgs = args.Where(a => a != "--interactive").ToArray();

        try
        {
            // Rebuild the full path correctly handling quotes
            string fullPath = string.Join(" ", filteredArgs);
            LogMessage($"Raw arguments value: {fullPath}");
            
            // First argument is the full path to the game, potentially with spaces
            string romPath = fullPath.Trim();
            
            // Clean path from quotes if present
            romPath = romPath.Trim('"');
            LogMessage($"Cleaned ROM path: {romPath}");
            
            // Extract game name from full path
            
            // Extract game name (last path segment without extension)
            string gameName = Path.GetFileName(romPath);
            LogMessage($"Extracted filename: {gameName}");
            
            // If path contains 'roms' (case insensitive), take the following segment
            int romsIndex = romPath.IndexOf("roms", StringComparison.OrdinalIgnoreCase);
            if (romsIndex >= 0)
            {
                string afterRoms = romPath.Substring(romsIndex + 4); // +4 for "roms" length
                LogMessage($"Path after 'roms': {afterRoms}");

                // Clean and divide the path
                var pathParts = afterRoms.Trim('\\', '/', ' ').Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

                // If we have at least part of a path after 'roms', we take it as the game name.
                if (pathParts.Length > 0)
                {
                    // If path contains 'steam', take the full filename
                    if (afterRoms.Trim('\\', '/').StartsWith("steam", StringComparison.OrdinalIgnoreCase))
                    {
                        // For Steam games, take the full filename (without extension)
                        string fileName = Path.GetFileName(romPath);
                        gameName = Path.GetFileNameWithoutExtension(fileName);
                    }
                    else if (pathParts.Length > 0)
                    {
                        // For other cases, take the last path segment
                        gameName = pathParts[pathParts.Length - 1];
                    }

                    // Remove the extension if present
                    gameName = Path.GetFileNameWithoutExtension(gameName);
                    LogMessage($"Extracted game name: {gameName}");
                }
            }

            LogMessage($"Searching image for game: {gameName}");
            Console.WriteLine($"Searching image for game: {gameName}");

            // Folder containing the ISO images (same folder as the executable)
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            string isoFolder = Path.Combine(appPath, "iso");
            LogMessage($"Images folder: {isoFolder}");

            // Create directory if it doesn't exist
            if (!Directory.Exists(isoFolder))
            {
                try
                {
                    LogMessage("Creating ISO folder as it doesn't exist");
                    Directory.CreateDirectory(isoFolder);
                    string message = $"The folder {isoFolder} has been created. Please place your disk images there.";
                    LogMessage(message);
                    Console.WriteLine(message);
                    return 1;
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Unable to create the folder {isoFolder}: {ex.Message}";
                    LogMessage(errorMsg, true);
                    Console.WriteLine(errorMsg);
                    return 1;
                }
            }

            // Check if ImgDrive is installed
            bool canUseImgDrive = IsImgDriveInstalled();

            // Define the supported formats based on the availability of ImgDrive
            var supportedFormats = canUseImgDrive 
                ? _supportedImageFormats  // All formats if ImgDrive is installed
                : new[] { ".iso" };      // Only ISO if ImgDrive is not installed

            if (!canUseImgDrive)
            {
                LogMessage("ImgDrive is not installed. Only the ISO format is supported.", true);
            }
            else
            {
                LogMessage("ImgDrive is installed. All image formats are supported.");
            }

            // Search for image file matching the game name
            var searchPatterns = supportedFormats.SelectMany(f => new[] 
                {
                    $"CD{f}",
                    $"{gameName}{f}",
                    $"{gameName} (Disc 1){f}",
                    $"{gameName} (Disc 2){f}",
                    $"{gameName} (Disc 3){f}",
                    $"{gameName} (Disc 4){f}",
                    $"Disc 1 of {gameName}{f}",
                    $"Disc 2 of {gameName}{f}",
                    $"Disc 3 of {gameName}{f}",
                    $"Disc 4 of {gameName}{f}"
                });

            // For .cue files, we also check the corresponding .bin file.
            if (canUseImgDrive && searchPatterns.Any(p => p.EndsWith(".cue")))
            {
                searchPatterns = searchPatterns.Concat(searchPatterns
                    .Where(p => p.EndsWith(".cue"))
                    .Select(p => p.Replace(".cue", ".bin")));
            }

            string[] matchingFiles = searchPatterns
                .SelectMany(pattern => Directory.GetFiles(isoFolder, pattern, SearchOption.TopDirectoryOnly))
                .ToArray();

            if (matchingFiles.Length == 0)
            {
                string errorMsg = $"No image found for game: {gameName}";
                LogMessage(errorMsg, true);
                Console.WriteLine(errorMsg);
                return 1;
            }

            string imagePath = matchingFiles[0];
            LogMessage($"Image found: {imagePath}");
            Console.WriteLine($"Image found: {imagePath}");

            // Try to mount the image
            try
            {
                bool mountSuccess = false;
                string extension = Path.GetExtension(imagePath).ToLowerInvariant();

                // If ImgDrive is available, use it for all types of images.
                if (canUseImgDrive)
                {
                    // For .bin files, we check if there is a corresponding .cue file.
                    if (extension == ".bin")
                    {
                        string cuePath = Path.ChangeExtension(imagePath, ".cue");
                        if (File.Exists(cuePath))
                        {
                            LogMessage($".cue file found, use it: {cuePath}");
                            imagePath = cuePath;
                        }
                    }
                    
                    LogMessage($"Attempting to mount with ImgDrive: {imagePath}");
                    mountSuccess = MountWithImgDrive(imagePath);
                }
                // Alternatively, use native Windows mounting only for ISOs.
                else if (extension == ".iso")
                {
                    LogMessage($"Attempting native Windows mounting: {imagePath}");
                    mountSuccess = MountIso(imagePath);
                }
                else
                {
                    string errorMsg = "Image format not supported without ImgDrive.";
                    LogMessage(errorMsg, true);
                    Console.WriteLine(errorMsg);
                    return 1;
                }

                if (mountSuccess)
                {
                        // Save the mounted image path.
                        try
                        {
                            SaveMountedImage(imagePath);
                            LogMessage("Mount information saved");
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error saving mount state: {ex.Message}";
                            LogMessage(errorMsg, true);
                            Console.WriteLine(errorMsg);
                        }
                        
                        // In non-interactive mode (default), exit immediately.
                        LogMessage("Non-interactive mode, exiting immediately");
                        
                        // Interactive mode only if explicitly requested.
                        if (interactive && Environment.UserInteractive)
                        {
                            LogMessage("Interactive mode detected, waiting for key press...");
                            Console.WriteLine("Press any key to unmount and exit...");
                            Console.ReadKey();
                            return UnmountImage();
                        }
                        
                        return 0; // Success
                }
                    else
                    {
                        string errorMsg = "Failed to mount image";
                        LogMessage(errorMsg, true);
                        Console.WriteLine(errorMsg);
                        return 1;
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error mounting image: {ex.Message}";
                    LogMessage(errorMsg, true);
                    Console.WriteLine(errorMsg);
                    return 1;
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Unexpected error: {ex.Message}";
                LogMessage(errorMsg, true);
                Console.WriteLine(errorMsg);
                return 1;
            }
            
            return 0; // End of program
    }
}
