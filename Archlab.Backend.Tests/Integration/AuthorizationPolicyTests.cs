using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// The AdminOnly and AdminOrManager policies decide who can create users, edit categories, and
/// cancel sales. They are declared on the endpoints, so no service test can reach them.
/// </summary>
public class AuthorizationPolicyTests
{
    [Fact]
    public async Task Admin_acessa_a_lista_de_usuarios()
    {
        await using var api = await ApiFactory.CreateAsync();
        var token = await api.AdminTokenAsync();

        var response = await Get(api, "/api/users", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_recebe_403_na_lista_de_usuarios()
    {
        await using var api = await ApiFactory.CreateAsync();
        var operatorToken = await CreateOperatorAsync(api);

        var response = await Get(api, "/api/users", operatorToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// 401 and 403 are different answers: the first says "identify yourself", the second says
    /// "you are identified and still may not". Collapsing them would hide a broken policy behind
    /// what looks like a login problem.
    /// </summary>
    [Fact]
    public async Task Sem_token_a_lista_de_usuarios_devolve_401_e_nao_403()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Operator_ainda_acessa_o_que_nao_exige_politica()
    {
        await using var api = await ApiFactory.CreateAsync();
        var operatorToken = await CreateOperatorAsync(api);

        var response = await Get(api, "/api/categories", operatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> CreateOperatorAsync(ApiFactory api)
    {
        const string username = "caixa01";
        const string password = "Op3rador!Teste";

        var adminToken = await api.AdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { username, password, role = "Operator" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var created = await api.Client.SendAsync(request);
        created.EnsureSuccessStatusCode();

        return await api.TokenForAsync(username, password);
    }

    private static Task<HttpResponseMessage> Get(ApiFactory api, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return api.Client.SendAsync(request);
    }
}
