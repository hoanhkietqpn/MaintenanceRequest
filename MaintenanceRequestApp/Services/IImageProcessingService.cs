using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MaintenanceRequestApp.Services
{
    public interface IImageProcessingService
    {
        Task<string> ProcessAndSaveImageAsync(IFormFile imageFile, string uploadFolder);
        Task<List<string>> ProcessAndSaveMultipleImagesAsync(List<IFormFile> imageFiles, string uploadFolder);
    }
}
