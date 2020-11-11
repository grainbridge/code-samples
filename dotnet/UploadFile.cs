using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace GrainBridge
{
  public class UploadPolicy
  {
    public string url {get;set;}
    public string keyPrefix {get;set;}

    public Dictionary<string, string> fields {get;set;} 
  }

  public class FileUploader
  {
    public static string UploadFile(string filePath, UploadPolicy uploadPolicy) 
    {      
      string fileName = Path.GetFileName(filePath);
      string key = uploadPolicy.keyPrefix + fileName;
      Uri uri = new Uri(uploadPolicy.url);
      HttpWebRequest webRequest = WebRequest.Create(uri) as HttpWebRequest;

      var boundary = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('=', 'z');
      webRequest.ContentType = string.Format(CultureInfo.InvariantCulture, "multipart/form-data; boundary={0}", boundary);
      webRequest.Method = "POST";
      webRequest.Timeout = 900000; // 15 Mins

      using (var reqStream = webRequest.GetRequestStream())
      {
          WriteFormDatum(reqStream, "key", key, boundary);
          foreach (string field in uploadPolicy.fields.Keys)
          {
              WriteFormDatum(reqStream, field, uploadPolicy.fields[field], boundary);
          }

          byte[] boundaryBytes = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "--{0}\r\nContent-Disposition: form-data; name=\"file\"\r\n\r\n", boundary));

          reqStream.Write(boundaryBytes, 0, boundaryBytes.Length);

          using (var inputStream = File.OpenRead(filePath))
          {
              byte[] buf = new byte[1024];
              int bytesRead;
              while ((bytesRead = inputStream.Read(buf, 0, 1024)) > 0)
              {
                  reqStream.Write(buf, 0, bytesRead);
              }
          }

          byte[] endBoundaryBytes = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "\r\n--{0}--", boundary));

          reqStream.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);
      }
      HttpWebResponse response = null;
      try
      {
          response = webRequest.GetResponse() as HttpWebResponse;
          // return the file key
          return key;
      }
      catch (WebException ex)
      {
          string responseText = "";
          try
          {
              using (var reader = new System.IO.StreamReader(ex.Response.GetResponseStream()))
              {
                  responseText = reader.ReadToEnd();                
              }
          }
          catch 
          {
              throw ex;
          }

          throw new Exception(responseText, ex);
      }
      finally 
      {
          response.Dispose();
      }
    }

    private static void WriteFormDatum(Stream stream, string name, string value, string boundary)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}\r\n", boundary, name, value));
        stream.Write(bytes, 0, bytes.Length);
    }
  }

    class Program
    {
        static void Main(string[] args)
        {
            var jsonResp = "{\"url\":\"https://s3.amazonaws.com/nitro-files\",\"fields\":{\"x-amz-server-side-encryption\":\"AES256\",\"bucket\":\"sandbox-files\",\"X-Amz-Algorithm\":\"AWS4-HMAC-SHA256\",\"X-Amz-Credential\":\"ASIATCVWOBXXXXXX/20201026/us-east-1/s3/aws4_request\",\"X-Amz-Date\":\"20201026T203426Z\",\"X-Amz-Security-Token\":\"IQoJb3JpZ2luX2VjEK3//////////wEaCX....9XX0=\",\"X-Amz-Signature\":\"9a4...2f\"},\"keyPrefix\":\"documents/ADM/bulk/ad822ac424e/\"}";
            var uploadPolicy = JsonSerializer.Deserialize<UploadPolicy>(jsonResp);
            string fileKey = FileUploader.UploadFile("settlement.csv", uploadPolicy);
            Console.WriteLine("File key is " + fileKey);
        }        
    }
}
