using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using timelapse.core.models; // Add this import

namespace timelapse.functions.services; // Add namespace

public class VideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;

    public VideoProcessingService(ILogger<VideoProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<string> CreateTimelapseAsync(
        Device device, 
        List<string> imagePaths, 
        DateTime startTime, 
        DateTime endTime,
        string description = null)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "timelapse", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var outputPath = Path.Combine(tempDirectory, "timelapse.mp4");
            
            // Create image list file and prepare timestamp data
            var (imageListPath, timestamps) = await CreateImageListFileWithTimestamps(imagePaths, tempDirectory);
            
            // Build ffmpeg command
            var ffmpegArgs = BuildFFmpegCommand(imageListPath, outputPath, device, timestamps, description, tempDirectory);
            
            // Execute ffmpeg
            await ExecuteFFmpegAsync(ffmpegArgs);
            
            return outputPath;
        }
        catch
        {
            // Cleanup on error
            if (Directory.Exists(tempDirectory))
            {
//                Directory.Delete(tempDirectory, true);
            }
            throw;
        }
    }

    private async Task<(string, List<string>)> CreateImageListFileWithTimestamps(List<string> imagePaths, string tempDirectory)
    {
        var listFilePath = Path.Combine(tempDirectory, "images.txt");
        var content = new StringBuilder();
        var timestamps = new List<string>();
        
        foreach (var imagePath in imagePaths)
        {
            content.AppendLine($"file '{imagePath}'");
            content.AppendLine("duration 0.033"); // ~30fps
            
            // Extract timestamp from filename
            var filename = Path.GetFileNameWithoutExtension(imagePath);
            var timestamp = ExtractTimestampFromFilename(filename);
            timestamps.Add(timestamp);
        }
        
        await File.WriteAllTextAsync(listFilePath, content.ToString());
        
        return (listFilePath, timestamps);
    }
    
    private string ExtractTimestampFromFilename(string filename)
    {
        try
        {
            // Format: nn_YYYY-MM-DD_hhmmss
            // Example: 18_2026-01-18_103810
            var parts = filename.Split('_');
            if (parts.Length >= 3)
            {
                var date = parts[1]; // YYYY-MM-DD
                var time = parts[2]; // hhmmss
                
                // Format time as hh:mm:ss (no escaping needed here, will be in text file)
                if (time.Length >= 6)
                {
                    var formattedTime = $"{time.Substring(0, 2)}:{time.Substring(2, 2)}:{time.Substring(4, 2)}";
                    return $"{date} {formattedTime}";
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails, return empty string
        }
        
        return string.Empty;
    }

    private string BuildFFmpegCommand(string imageListPath, string outputPath, Device device, List<string> timestamps, string description, string tempDirectory)
    {
        // Create an ASS subtitle file with per-frame timestamps
        var subsFile = Path.Combine(tempDirectory, "subtitles.ass");
        CreateSubtitleFile(subsFile, device, timestamps, description);
        
        var args = new StringBuilder();
        
        // Input video and subtitles
        args.Append($"-f concat -safe 0 -i \"{imageListPath}\" ");
        
        // Video filters
        var filters = new List<string>();
        
        // Scale and format
        filters.Add("scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2");
        
        // Burn in subtitles
        filters.Add($"subtitles='{subsFile.Replace("'", "'\\''")}'");
        
        // Apply filters
        args.Append($"-vf \"{string.Join(",", filters)}\" ");
        
        // Encoding settings
        args.Append("-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p ");
        args.Append("-r 30 "); // 30fps output
        args.Append($"\"{outputPath}\"");
        
        return args.ToString();
    }

    private void CreateSubtitleFile(string subsFile, Device device, List<string> timestamps, string description)
    {
        var content = new StringBuilder();
        
        // ASS subtitle format header
        content.AppendLine("[Script Info]");
        content.AppendLine("ScriptType: v4.00+");
        content.AppendLine("PlayResX: 1920");
        content.AppendLine("PlayResY: 1080");
        content.AppendLine();
        content.AppendLine("[V4+ Styles]");
        content.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
        // White text, black background box
        content.AppendLine("Style: Default,Adwaita Mono,20,&H00FFFFFF,&H000000FF,&H00000000,&HB2000000,-1,0,0,0,100,100,0,0,3,2,0,7,10,10,10,1");
        content.AppendLine();
        content.AppendLine("[Events]");
        content.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");
        
        // Calculate duration per frame (0.033s = ~30fps)
        double frameDuration = 0.033;
        
        for (int i = 0; i < timestamps.Count; i++)
        {
            double startTime = i * frameDuration;
            double endTime = (i + 1) * frameDuration;
            
            // Format times as HH:MM:SS.CC
            var startTimeStr = TimeSpan.FromSeconds(startTime).ToString(@"h\:mm\:ss\.ff");
            var endTimeStr = TimeSpan.FromSeconds(endTime).ToString(@"h\:mm\:ss\.ff");
            
            // Build the text for this frame
            var textLines = new List<string>();
            textLines.Add($"Camera: {device.Name}");
            textLines.Add($"Location: {device.Description}");
            if (!string.IsNullOrEmpty(description))
            {
                textLines.Add($"Event: {description}");
            }
            textLines.Add($"Time: {timestamps[i]}");
            
            // Join with \N for ASS newlines
            var text = string.Join("\\N", textLines);
            
            content.AppendLine($"Dialogue: 0,{startTimeStr},{endTimeStr},Default,,0,0,0,,{text}");
        }
        
        File.WriteAllText(subsFile, content.ToString());
        _logger.LogInformation("Created subtitle file with {Count} frames at {Path}", timestamps.Count, subsFile);
    }

    private void CreateTextMetadataFile(string metadataFile, Device device, List<string> timestamps, string description)
    {
        // No longer needed, kept for compatibility
    }

    private string BuildDynamicOverlayText(Device device, string description, string textFile)
    {
        // No longer needed, kept for compatibility
        return "";
    }

    private async Task ExecuteFFmpegAsync(string arguments)
    {
        _logger.LogInformation("Executing FFmpeg with arguments: {Arguments}", arguments);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        
        var errorBuilder = new StringBuilder();
        process.ErrorDataReceived += (sender, e) => {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("FFmpeg failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new Exception($"FFmpeg processing failed: {error}");
        }
        
        _logger.LogInformation("FFmpeg completed successfully");
    }
}
