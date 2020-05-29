using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using UploadFile.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Server.IIS.Core;

namespace UploadFile.Controllers
{
    // https://wakeupandcode.com/azure-blob-storage-from-asp-net-core-file-upload/
    public class HomeController : Controller
    {
        private IConfiguration _configuration;
        private readonly IHttpClientFactory clientFactory;

        public HomeController(
            IConfiguration Configuration
            , IHttpClientFactory clientFactory)
        {
            _configuration = Configuration;
            this.clientFactory = clientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }

        // https://localhost:5001/uploadfilebyurl?url=http://img.txooo.com/2020/05/08/2b5b9903278597812939d96879cb3ec2.jpg
        [HttpGet("UploadFileByUrl")]
        public async Task<IActionResult> UploadFileByUrl([FromQuery]string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest();

            var client = clientFactory.CreateClient("TxFile");
            var response = await client.GetStringAsync("UpLoadForByte.ashx?tx_down_url=" + url);
            if (response.Equals("Error", StringComparison.CurrentCultureIgnoreCase))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "远程服务器异常");
            }
            return Ok(response.Replace("http:", "https:"));
        }

        //private IActionResult Json(bool success, string msg)
        //{
        //    return new JsonResult(new { success, msg });
        //}

        [HttpPost("UploadFiles")]
        //OPTION A: 禁用Asp.Net Core的默认上载大小限制
        [DisableRequestSizeLimit]
        //OPTION B: 取消注释以设置指定的上载文件限制
        //[RequestSizeLimit(40000000)] 

        public async Task<IActionResult> Post(List<IFormFile> files)
        {
            var uploadSuccess = false;
            string uploadedUri = null;

            foreach (var formFile in files)
            {
                if (formFile.Length <= 0)
                {
                    continue;
                }

                // NOTE: 取消对选项A或选项B的注释以使用一种方法而不是另一种方法

                ////OPTION A: convert to byte array before upload
                //using (var ms = new MemoryStream())
                //{
                //    formFile.CopyTo(ms);
                //    var fileBytes = ms.ToArray();
                //    (uploadSuccess, uploadedUri) = await UploadToTx(formFile.FileName, fileBytes);
                //}

                // OPTION B: read directly from stream for blob upload      
                using (var stream = formFile.OpenReadStream())
                {
                    //(uploadSuccess, uploadedUri) = await UploadToBlob(formFile.FileName, null, stream);
                    (uploadSuccess, uploadedUri) = await UploadToTx(formFile.FileName, stream);
                    TempData["uploadedUri"] = uploadedUri;
                }

            }

            if (uploadSuccess)
                return View("UploadSuccess");
            else
                return View("UploadError");
        }

        private async Task<(bool, string)> UploadToTx(string filename, Stream stream = null)
        {
            try
            {
                if ( stream == null)
                {
                    return (false, null);
                }
                using (HttpContent content = new StreamContent(stream))
                {
                    content.Headers.Add("TxoooUploadFileType", Path.GetExtension(filename).ToUpper());
                    var client = clientFactory.CreateClient("TxFile");
                    var response = await client.PostAsync("UpLoadForByte.ashx", content);
                    if (response != null && response.IsSuccessStatusCode)
                    {
                        var responseStr = await response.Content.ReadAsStringAsync();
                        if (responseStr.Equals("Error", StringComparison.CurrentCultureIgnoreCase))
                        {
                            return (false, "远程服务器异常");
                        }
                        return (true, responseStr.Replace("http:", "https:"));
                    }
                    else
                    {
                        return (false, $"远程服务器请求失败，{response.StatusCode}");
                    }
                }
            }
            catch (StorageException ex)
            {
                return (false, null);
            }
            finally
            {
                // OPTIONAL: Clean up resources, e.g. blob container
                //if (cloudBlobContainer != null)
                //{
                //    await cloudBlobContainer.DeleteIfExistsAsync();
                //}
            }
        }

        private async Task<(bool, string)> UploadToBlob(string filename, byte[] imageBuffer = null, Stream stream = null)
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            string storageConnectionString = _configuration["storageconnectionstring"];

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Create a container called 'uploadblob' and append a GUID value to it to make the name unique. 
                    cloudBlobContainer = cloudBlobClient.GetContainerReference("uploadblob" + Guid.NewGuid().ToString());
                    await cloudBlobContainer.CreateAsync();

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);

                    // Get a reference to the blob address, then upload the file to the blob.
                    CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);

                    if (imageBuffer != null)
                    {
                        // OPTION A: use imageBuffer (converted from memory stream)
                        await cloudBlockBlob.UploadFromByteArrayAsync(imageBuffer, 0, imageBuffer.Length);
                    }
                    else if (stream != null)
                    {
                        // OPTION B: pass in memory stream directly
                        await cloudBlockBlob.UploadFromStreamAsync(stream);
                    }
                    else
                    {
                        return (false, null);
                    }

                    return (true, cloudBlockBlob.SnapshotQualifiedStorageUri.PrimaryUri.ToString());
                }
                catch (StorageException ex)
                {
                    return (false, null);
                }
                finally
                {
                    // OPTIONAL: Clean up resources, e.g. blob container
                    //if (cloudBlobContainer != null)
                    //{
                    //    await cloudBlobContainer.DeleteIfExistsAsync();
                    //}
                }
            }
            else
            {
                return (false, null);
            }

        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
