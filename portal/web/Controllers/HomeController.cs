using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace Web.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync("/api/products");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var products = JsonSerializer.Deserialize<List<Contracts.DTOs.ProductDto>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return View(products);
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            ViewBag.Error = "Unable to load products. Please try again later.";
        }
        
        return View(new List<Contracts.DTOs.ProductDto>());
    }

    public async Task<IActionResult> ProductMappings(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"/api/products/{id}/mappings");
            var typesResponse = await client.GetAsync("/api/products/supported-data-types");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var mappings = JsonSerializer.Deserialize<Contracts.DTOs.ProductMappingsDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (typesResponse.IsSuccessStatusCode)
                {
                    var typesJson = await typesResponse.Content.ReadAsStringAsync();
                    var types = JsonSerializer.Deserialize<string[]>(typesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<string>();
                    ViewBag.SupportedTypes = types;
                }
                ViewBag.ProductId = id;
                ViewBag.ApiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:7001/";
                
                return View(mappings);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            ViewBag.Error = "Unable to load product mappings. Please try again later.";
        }
        
        return View(null);
    }

    [HttpPost]
    public async Task<IActionResult> RefreshSchema(Guid id)
    {
        try
        {
            Console.WriteLine($"[RefreshSchema] Starting refresh for ProductId: {id}");
            
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.PostAsync($"/api/products/{id}/refresh-schema", null);
            
            Console.WriteLine($"[RefreshSchema] API Response Status: {response.StatusCode}");
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[RefreshSchema] API Error: {errorContent}");
                TempData["Error"] = $"Failed to refresh schema: {response.StatusCode}";
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[RefreshSchema] API Success Response: {content}");
                TempData["Success"] = "Schema refreshed successfully";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RefreshSchema] Exception: {ex.Message}");
            Console.WriteLine($"[RefreshSchema] Stack: {ex.StackTrace}");
            TempData["Error"] = $"Error refreshing schema: {ex.Message}";
        }
        return RedirectToAction(nameof(ProductMappings), new { id });
    }

    public async Task<IActionResult> ProductDetails(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"/api/products/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var product = JsonSerializer.Deserialize<Contracts.DTOs.ProductDto>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                ViewBag.ApiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:7001/";
                return View(product);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound();
            }
        }
        catch (Exception ex)
        {
            ViewBag.Error = "Unable to load product details. Please try again later.";
        }
        
        return View(null);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProduct(Guid id, Contracts.DTOs.ProductDto model)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = JsonSerializer.Serialize(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/products/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Product updated successfully.";
                return RedirectToAction(nameof(ProductDetails), new { id });
            }
            else
            {
                TempData["Error"] = "Failed to update product.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = "An error occurred while updating the product.";
        }
        
        return RedirectToAction(nameof(ProductDetails), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> TestConnection(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"/api/products/{id}/test-connection");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<dynamic>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                return Json(result);
            }
            else
            {
                return Json(new { success = false, message = "Connection test failed" });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error testing connection" });
        }
    }

    // Connection String proxy endpoints
    [HttpGet]
    public async Task<IActionResult> GetConnectionStrings(Guid productId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.GetAsync($"/api/ConnectionStrings/product/{productId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            
            // Return empty array if not found
            return Content("[]", "application/json");
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            Console.WriteLine($"Error in GetConnectionStrings: {ex.Message}");
            return Content("[]", "application/json");
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveConnectionString([FromBody] JsonElement data)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = data.GetRawText();
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/ConnectionStrings", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return BadRequest(errorContent ?? "Failed to save connection string");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }
    }
    
    [HttpPut]
    public async Task<IActionResult> UpdateConnectionString(Guid id, [FromBody] JsonElement data)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = data.GetRawText();
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"/api/ConnectionStrings/{id}", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return BadRequest(errorContent ?? "Failed to update connection string");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }
    }
    
    [HttpDelete]
    public async Task<IActionResult> DeleteConnectionString(Guid id)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var response = await client.DeleteAsync($"/api/ConnectionStrings/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                return Ok();
            }
            
            return BadRequest("Failed to delete connection string");
        }
        catch
        {
            return BadRequest("Error deleting connection string");
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> TestConnectionString([FromBody] JsonElement data)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = data.GetRawText();
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/ConnectionStrings/test", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = errorContent ?? "Connection test failed" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> TestStoredConnectionString([FromBody] JsonElement data)
    {
        try
        {
            // Extract the ID from the JSON data
            if (data.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetString();
                var client = _httpClientFactory.CreateClient("API");
                var response = await client.PostAsync($"/api/ConnectionStrings/{id}/test", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return Content(result, "application/json");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                return Json(new { isSuccessful = false, errorMessage = errorContent ?? "Connection test failed" });
            }
            
            return BadRequest("Connection string ID not provided");
        }
        catch (Exception ex)
        {
            return Json(new { isSuccessful = false, errorMessage = ex.Message });
        }
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] JsonElement data)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var json = data.GetRawText();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync("/api/products", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return Content(result, "application/json");
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorContent);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    public IActionResult Error()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateMapping(Guid productId, Guid columnId, string? obfuscationDataType, bool isEnabled, bool preserveLength)
    {
        bool ok = false;
        try
        {
            var client = _httpClientFactory.CreateClient("API");
            var normalizedType = string.IsNullOrWhiteSpace(obfuscationDataType) || obfuscationDataType == "None" ? null : obfuscationDataType;
            var payload = new
            {
                ObfuscationDataType = normalizedType,
                IsEnabled = isEnabled,
                PreserveLength = preserveLength,
                IsManuallyConfigured = true
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"/api/products/{productId}/columns/{columnId}/mapping", content);
            ok = resp.IsSuccessStatusCode;
            return Ok(new { success = ok, mapped = normalizedType != null && isEnabled, obfuscationDataType = normalizedType });
        }
        catch
        {
            return Ok(new { success = ok, mapped = false, obfuscationDataType = (string?)null });
        }
    }
}
