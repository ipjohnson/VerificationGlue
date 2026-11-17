using System.Net.Http.Json;
using VerificationGlue.Mocks;
using VerificationGlue.Tests;
using static VerificationGlue.VerifySyntax;

namespace ShopApi.Tests;

[WebApp<ShopApiEntry>]
public partial class ManyTests
{
    [VerifyTest]
    public async Task Test00(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test01(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test02(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test03(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test04(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test05(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test06(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test07(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test08(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test09(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test10(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test11(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test12(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test13(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test14(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test15(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test16(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test17(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test18(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test19(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test20(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test21(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test22(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test23(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test24(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test25(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test26(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test27(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test28(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test29(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test30(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test31(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test32(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test33(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test34(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test35(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test36(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test37(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test38(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }

    [VerifyTest]
    public async Task Test39(HttpClient client, [Mock] IClock clock)
    {
        var orders = await client.GetFromJsonAsync<Order[]>("/orders");

        Verify(orders!.Length == 0);
    }
}
