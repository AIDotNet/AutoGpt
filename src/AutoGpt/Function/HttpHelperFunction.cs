using System.ComponentModel;
using System.Text;

using Microsoft.SemanticKernel;

namespace AutoGpt.Function;

public class HttpHelperFunction
{
    private readonly HttpClient _httpClient = new();

    /// <summary>
    /// Get请求
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <returns></returns>
    [KernelFunction, Description("Get请求")]
    public async Task<string> Get(string url)
    {
        return await _httpClient.GetStringAsync(url);
    }


    /// <summary>
    /// Post请求
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <param name="content">请求内容</param>
    /// <returns></returns>
    [KernelFunction, Description("Post请求")]
    public async Task<string> PostAsync(string url, string content)
    {
        var httpContent = new StringContent(content, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, httpContent);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Del请求
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <returns></returns>
    [KernelFunction, Description("Del请求")]
    public async Task<string> DeleteAsync(string url)
    {
        var response = await _httpClient.DeleteAsync(url);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Put请求
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <param name="content">请求内容</param>
    /// <returns></returns>
    [KernelFunction, Description("Put请求")]
    public async Task<string> PutAsync(string url, string content)
    {
        var httpContent = new StringContent(content, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(url, httpContent);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Patch请求
    /// </summary>
    /// <param name="url">请求地址</param>
    /// <param name="content">请求内容</param>
    /// <returns></returns>
    [KernelFunction, Description("Patch请求")]
    public async Task<string> PatchAsync(string url, string content)
    {
        var httpContent = new StringContent(content, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = httpContent };
        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}