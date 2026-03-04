using cwApp.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace cwApp.Services;

public class ConfigStorageService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ConfigStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<ConnectWiseConfig> LoadAsync()
    {
        var config = new ConnectWiseConfig();

        config.CompanyId = await GetItemAsync("cw_company_id") ?? "Intwo";
        config.PublicKey = await GetItemAsync("cw_public_key") ?? "";
        config.PrivateKey = await GetItemAsync("cw_private_key") ?? "";
        config.SiteUrl = await GetItemAsync("cw_site_url") ?? "connect.intwo.cloud";
        config.MemberId = await GetItemAsync("cw_member_id") ?? "";
        config.WorkType = await GetItemAsync("cw_work_type") ?? "Remote-Standard";
        config.BillableOption = await GetItemAsync("cw_billable_option") ?? "DoNotBill";
        config.ClientId = await GetItemAsync("cw_client_id") ?? "4332716b-7270-470d-b7c6-9c036f760e6f";

        var tzStr = await GetItemAsync("cw_timezone_offset");
        config.TimezoneOffset = double.TryParse(tzStr, out var tz) ? tz : -4.0;

        return config;
    }

    public async Task SaveAsync(ConnectWiseConfig config)
    {
        await SetItemAsync("cw_company_id", config.CompanyId);
        await SetItemAsync("cw_public_key", config.PublicKey);
        await SetItemAsync("cw_private_key", config.PrivateKey);
        await SetItemAsync("cw_site_url", config.SiteUrl);
        await SetItemAsync("cw_member_id", config.MemberId);
        await SetItemAsync("cw_work_type", config.WorkType);
        await SetItemAsync("cw_billable_option", config.BillableOption);
        await SetItemAsync("cw_client_id", config.ClientId);
        await SetItemAsync("cw_timezone_offset", config.TimezoneOffset.ToString());
    }

    private async Task<string?> GetItemAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch { return null; }
    }

    private async Task SetItemAsync(string key, string? value)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", key, value ?? "");
        }
        catch { }
    }
}
