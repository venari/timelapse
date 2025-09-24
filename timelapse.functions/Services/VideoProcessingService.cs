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
            
            // Create image list file for ffmpeg
            var imageListPath = await CreateImageListFile(imagePaths, tempDirectory);
            
            // Build ffmpeg command
            var ffmpegArgs = BuildFFmpegCommand(imageListPath, outputPath, device, startTime, endTime, description);
            
            // Execute ffmpeg
            await ExecuteFFmpegAsync(ffmpegArgs);
            
            return outputPath;
        }
        catch
        {
            // Cleanup on error
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
            throw;
        }
    }

    private async Task<string> CreateImageListFile(List<string> imagePaths, string tempDirectory)
    {
        var listFilePath = Path.Combine(tempDirectory, "images.txt");
        var content = new StringBuilder();
        
        foreach (var imagePath in imagePaths)
        {
            content.AppendLine($"file '{imagePath}'");
            content.AppendLine("duration 0.033"); // ~30fps
        }
        
        await File.WriteAllTextAsync(listFilePath, content.ToString());
        return listFilePath;
    }

    private string BuildFFmpegCommand(string imageListPath, string outputPath, Device device, DateTime startTime, DateTime endTime, string description)
    {
        var args = new StringBuilder();
        
        // Input
        args.Append($"-f concat -safe 0 -i \"{imageListPath}\" ");
        
        // Video filters
        var filters = new List<string>();
        
        // Scale and format
        filters.Add("scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2");
        
        // Build overlay text
        var overlayText = BuildOverlayText(device, startTime, endTime, description);
        filters.Add(overlayText);
        
        // Apply filters
        args.Append($"-vf \"{string.Join(",", filters)}\" ");
        
        // Encoding settings
        args.Append("-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p ");
        args.Append("-r 30 "); // 30fps output
        args.Append($"\"{outputPath}\"");
        
        return args.ToString();
    }

    private string BuildOverlayText(Device device, DateTime startTime, DateTime endTime, string description)
    {
        var textLines = new List<string>();
        
        textLines.Add($"Camera: {device.Name}");
        
        if (!string.IsNullOrEmpty(description))
        {
            textLines.Add($"Event: {description}");
        }
        
        textLines.Add($"Period: {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}");
        
        var text = string.Join("\\n", textLines);
        
        return $"drawtext=fontfile='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf':" +
               $"text='{text}':" +
               $"fontcolor=white:fontsize=20:box=1:boxcolor=black@0.7:boxborderw=5:" +
               $"x=10:y=10:line_spacing=5";
    }

    private async Task ExecuteFFmpegAsync(string arguments)
    {
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
            _logger.LogError("FFmpeg failed: {Error}", error);
            throw new Exception($"FFmpeg processing failed: {error}");
        }
        
        _logger.LogInformation("FFmpeg completed successfully");
    }
}