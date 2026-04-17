using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace MaintenanceRequestApp.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        // 20MB limit in bytes
        private const long MaxFileSizeInBytes = 20 * 1024 * 1024;
        private const int MaxWidth = 1920; 
        private const int MaxHeight = 1080;

        public async Task<string> ProcessAndSaveImageAsync(IFormFile imageFile, string uploadFolder)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("File không được trống.");

            if (imageFile.Length > MaxFileSizeInBytes)
                throw new ArgumentException("Kích thước file vượt quá 20MB.");

            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                throw new ArgumentException("Chỉ cho phép tải lên định dạng hình ảnh (.jpg, .jpeg, .png).");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var newFileName = $"{Guid.NewGuid()}.webp";
            var outputPath = Path.Combine(uploadFolder, newFileName);

            using (var memoryStream = new MemoryStream())
            {
                await imageFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using (var image = await Image.LoadAsync(memoryStream))
                {
                    // Resize if larger than max bounds
                    if (image.Width > MaxWidth || image.Height > MaxHeight)
                    {
                        var options = new ResizeOptions
                        {
                            Size = new Size(MaxWidth, MaxHeight),
                            Mode = ResizeMode.Max
                        };
                        image.Mutate(x => x.Resize(options));
                    }

                    // Save as WebP
                    var encoder = new WebpEncoder { Quality = 80 };
                    await image.SaveAsync(outputPath, encoder);
                }
            }

            return newFileName;
        }

        public async Task<List<string>> ProcessAndSaveMultipleImagesAsync(List<IFormFile> imageFiles, string uploadFolder)
        {
            if (imageFiles == null || imageFiles.Count > 3)
                throw new ArgumentException("Chỉ được phép tải lên tối đa 3 hình ảnh.");

            var savedFiles = new List<string>();
            foreach (var file in imageFiles)
            {
                var newFileName = await ProcessAndSaveImageAsync(file, uploadFolder);
                savedFiles.Add(newFileName);
            }

            return savedFiles;
        }
    }
}
