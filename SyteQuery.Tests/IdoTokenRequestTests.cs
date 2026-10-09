using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SyteQuery.Features.Environments.Services;
using Xunit;

namespace SyteQuery.Tests;

/// <summary>
/// The security-token request, which Infor offers in two forms: credentials in the URL
/// (/token/{config}/{username}/{password}) and credentials in headers (/token/{config}). Neither works in every
/// environment, so the client tries whichever is likely to work and falls back to the other.
/// </summary>
public class IdoTokenRequestTests
{
    private const string Host = "https://csi.example.test";
    private const string ExamplePassword = "example-pw";   // obviously fake; also what the secrets scan treats as a placeholder
    private const string Success = "{\"Success\":true,\"Message\":null,\"Token\":\"TOKEN-123\"}";
    private const string BadPassword = "{\"Success\":false,\"Message\":\"Invalid user name or password.\",\"Token\":null}";

    /// <summary>What a test server saw.</summary>
    private sealed record Seen(string Path, bool UsernameHeader, bool PasswordHeader, string? UsernameValue, string? PasswordValue)
    {
        public bool InUrl => Path.Split('/').Length > 4;   // .../token/{config}/{user}/{password}
        public bool InHeaders => UsernameHeader && PasswordHeader;
    }

    private sealed class FakeServer : HttpMessageHandler
    {
        private readonly Func<Seen, HttpResponseMessage> _respond;
        public List<Seen> Requests { get; } = new();

        public FakeServer(Func<Seen, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("username", out var user);
            request.Headers.TryGetValues("password", out var pass);
            var seen = new Seen(request.RequestUri!.AbsolutePath.Replace("/IDORequestService/ido", ""),
                user is not null, pass is not null, user?.FirstOrDefault(), pass?.FirstOrDefault());
            Requests.Add(seen);
            return Task.FromResult(_respond(seen));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("") };

    private static (IdoHttpClient client, FakeServer server) Client(Func<Seen, HttpResponseMessage> respond, IdoTokenStyleMemory? memory = null)
    {
        var server = new FakeServer(respond);
        return (new IdoHttpClient(new HttpClient(server), NullLogger<IdoHttpClient>.Instance, memory ?? new IdoTokenStyleMemory()), server);
    }

    private static Task<IdoTokenResult> Token(IdoHttpClient client, string user = "jdoe", string password = ExamplePassword, string config = "TRN") =>
        client.GetTokenAsync(Host, config, user, password);

    // ---- ordinary credentials: the URL form first, as the app always has ----

    [Fact]
    public async Task Ordinary_credentials_use_the_URL_form_and_stop_when_it_works()
    {
        var (client, server) = Client(_ => Json(Success));

        var result = await Token(client);

        Assert.True(result.Success);
        Assert.Equal("TOKEN-123", result.Token);
        var request = Assert.Single(server.Requests);
        Assert.Equal($"/token/TRN/jdoe/{ExamplePassword}", request.Path);
        Assert.False(request.InHeaders);
    }

    [Fact]
    public async Task The_REST_address_is_built_from_the_host_alone()
    {
        string? uri = null;
        var handler = new LambdaHandler(r => { uri = r.RequestUri!.ToString(); return Json(Success); });
        var client = new IdoHttpClient(new HttpClient(handler), NullLogger<IdoHttpClient>.Instance, new IdoTokenStyleMemory());

        await client.GetTokenAsync("https://csi.example.test/IDORequestService/RequestService.aspx", "TRN", "u", "p");

        Assert.Equal("https://csi.example.test/IDORequestService/ido/token/TRN/u/p", uri);
    }

    private sealed class LambdaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public LambdaHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_respond(request));
    }

    [Fact]
    public async Task Characters_that_are_legal_in_a_URL_but_special_are_percent_encoded()
    {
        string? raw = null;
        var client = new IdoHttpClient(new HttpClient(new LambdaHandler(r => { raw = r.RequestUri!.AbsoluteUri; return Json(Success); })),
            NullLogger<IdoHttpClient>.Instance, new IdoTokenStyleMemory());

        await client.GetTokenAsync(Host, "TRN", "jdoe", "p+a&s s!");

        Assert.EndsWith("/token/TRN/jdoe/p%2Ba%26s%20s%21", raw);
    }

    // ---- falling back ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task If_the_URL_form_gets_an_HTTP_error_the_header_form_is_tried(HttpStatusCode failure)
    {
        var (client, server) = Client(r => r.InHeaders ? Json(Success) : Status(failure));

        var result = await Token(client);

        Assert.True(result.Success);
        Assert.Equal(2, server.Requests.Count);
        Assert.True(server.Requests[0].InUrl);
        Assert.Equal("/token/TRN", server.Requests[1].Path);
        Assert.Equal("jdoe", server.Requests[1].UsernameValue);
        Assert.Equal(ExamplePassword, server.Requests[1].PasswordValue);
    }

    [Fact]
    public async Task A_web_page_instead_of_JSON_counts_as_no_answer_and_falls_back()
    {
        var (client, server) = Client(r => r.InHeaders
            ? Json(Success)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Please sign in</html>", Encoding.UTF8, "text/html") });

        var result = await Token(client);

        Assert.True(result.Success);
        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task JSON_that_is_not_a_token_answer_counts_as_no_answer()
    {
        var (client, server) = Client(r => r.InHeaders ? Json(Success) : Json("{\"hello\":\"world\"}"));

        Assert.True((await Token(client)).Success);
        Assert.Equal(2, server.Requests.Count);
    }

    // ---- a real rejection is final ----

    [Fact]
    public async Task A_wrong_password_is_answered_once_and_not_retried()
    {
        var (client, server) = Client(_ => Json(BadPassword));

        var result = await Token(client);

        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Invalid user name or password.", result.Message);
        Assert.Single(server.Requests);   // a second attempt would count as another failed login
    }

    [Fact]
    public async Task A_wrong_password_is_not_remembered_as_a_working_style()
    {
        var memory = new IdoTokenStyleMemory();
        var (client, _) = Client(_ => Json(BadPassword), memory);

        await Token(client);

        Assert.Null(memory.Get($"{Host}/IDORequestService/ido|TRN|jdoe"));
    }

    [Fact]
    public async Task A_network_failure_is_not_retried_in_the_other_form()
    {
        var calls = 0;
        var client = new IdoHttpClient(new HttpClient(new LambdaHandler(_ => { calls++; throw new HttpRequestException("No such host is known."); })),
            NullLogger<IdoHttpClient>.Instance, new IdoTokenStyleMemory());

        await Assert.ThrowsAsync<HttpRequestException>(() => Token(client));
        Assert.Equal(1, calls);
    }

    // ---- credentials a URL can't carry ----

    [Theory]
    [InlineData("DOMAIN\\jdoe", "secret")]     // backslash in the user name
    [InlineData("jdoe", "pa/ss")]              // slash
    [InlineData("jdoe", "what?")]              // question mark
    [InlineData("jdoe", "a#b")]                // hash
    [InlineData("jdoe", "100%")]               // percent
    [InlineData("jdoe", "..")]                 // path navigation
    public async Task Credentials_a_URL_path_would_mangle_go_in_headers_first_and_stay_out_of_the_URL(string user, string password)
    {
        var (client, server) = Client(_ => Json(Success));

        var result = await Token(client, user, password);

        Assert.True(result.Success);
        var request = Assert.Single(server.Requests);
        Assert.Equal("/token/TRN", request.Path);
        Assert.Equal(user, request.UsernameValue);
        Assert.Equal(password, request.PasswordValue);
    }

    [Fact]
    public async Task Special_credentials_fall_back_to_the_URL_form_if_headers_are_not_understood()
    {
        var (client, server) = Client(r => r.InHeaders ? Status(HttpStatusCode.NotFound) : Json(Success));

        Assert.True((await Token(client, "DOMAIN\\jdoe", "secret")).Success);
        Assert.Equal(2, server.Requests.Count);
        Assert.True(server.Requests[0].InHeaders);
        Assert.True(server.Requests[1].InUrl);
    }

    [Fact]
    public async Task Non_ASCII_credentials_are_never_sent_in_headers()
    {
        var (client, server) = Client(r => Json(Success));

        // an accented password AND a slash: the URL form is poor, but headers can't carry it reliably
        var result = await Token(client, "jdoe", "pässw/rd");

        Assert.True(result.Success);
        var request = Assert.Single(server.Requests);
        Assert.True(request.InUrl);
        Assert.False(request.InHeaders);
    }

    // ---- an environment set to one form: exactly that, once ----

    private static Task<IdoTokenResult> Forced(IdoHttpClient client, IdoTokenMode mode, string user = "jdoe", string password = ExamplePassword) =>
        client.GetTokenAsync(Host, "TRN", user, password, mode);

    [Fact]
    public async Task The_stored_numbers_for_the_modes_never_change()
    {
        // These values are written to users' databases. Reordering the enum would silently switch their setting.
        Assert.Equal(0, (int)IdoTokenMode.Auto);
        Assert.Equal(1, (int)IdoTokenMode.CredentialsInUrl);
        Assert.Equal(2, (int)IdoTokenMode.CredentialsInHeaders);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Set_to_the_URL_form_it_never_uses_headers_even_when_the_URL_form_fails()
    {
        var (client, server) = Client(r => r.InHeaders ? Json(Success) : Status(HttpStatusCode.NotFound));

        var result = await Forced(client, IdoTokenMode.CredentialsInUrl);

        Assert.False(result.Success);
        var request = Assert.Single(server.Requests);      // no fallback
        Assert.True(request.InUrl);
        Assert.Contains("credentials in the URL: HTTP 404", result.Message);
        Assert.Contains("\"Sign-in\"", result.Message);   // tells the user where to change it
    }

    [Fact]
    public async Task Set_to_headers_it_never_puts_credentials_in_the_URL_even_when_headers_fail()
    {
        var (client, server) = Client(r => r.InUrl ? Json(Success) : Status(HttpStatusCode.NotFound));

        var result = await Forced(client, IdoTokenMode.CredentialsInHeaders);

        Assert.False(result.Success);
        var request = Assert.Single(server.Requests);
        Assert.Equal("/token/TRN", request.Path);
        Assert.Equal("jdoe", request.UsernameValue);
        Assert.Contains("credentials in headers: HTTP 404", result.Message);
    }

    [Fact]
    public async Task Set_to_headers_it_works_for_the_user_whose_URL_form_does_not()
    {
        // The report behind this setting: the header form works, the URL form doesn't (same in Infor's own "Try it out").
        var (client, server) = Client(r => r.InHeaders ? Json(Success) : Json(BadPassword));

        var result = await Forced(client, IdoTokenMode.CredentialsInHeaders);

        Assert.True(result.Success);
        Assert.Equal("TOKEN-123", result.Token);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task Set_to_the_URL_form_with_a_wrong_password_is_one_request_with_the_servers_message()
    {
        var (client, server) = Client(_ => Json(BadPassword));

        var result = await Forced(client, IdoTokenMode.CredentialsInUrl);

        Assert.False(result.Success);
        Assert.Equal("Invalid user name or password.", result.Message);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task Set_to_headers_with_characters_a_header_cannot_carry_it_sends_nothing_and_says_why()
    {
        var (client, server) = Client(_ => Json(Success));

        var result = await Forced(client, IdoTokenMode.CredentialsInHeaders, password: "example-päss");

        Assert.False(result.Success);
        Assert.Empty(server.Requests);
        Assert.Contains("headers", result.Message);
        Assert.Contains("Automatic", result.Message);
    }

    [Fact]
    public async Task Set_to_the_URL_form_it_sends_special_credentials_as_asked_without_second_guessing()
    {
        var (client, server) = Client(_ => Json(Success));

        await Forced(client, IdoTokenMode.CredentialsInUrl, user: "DOMAIN\\jdoe");

        var request = Assert.Single(server.Requests);
        Assert.True(request.InUrl);
        Assert.False(request.InHeaders);
    }

    [Fact]
    public async Task A_forced_mode_neither_reads_nor_writes_the_memory()
    {
        var memory = new IdoTokenStyleMemory();
        memory.Remember($"{Host}/IDORequestService/ido|TRN|jdoe", IdoTokenStyle.CredentialsInHeaders);
        var (client, server) = Client(_ => Json(Success), memory);

        await Forced(client, IdoTokenMode.CredentialsInUrl);

        Assert.True(Assert.Single(server.Requests).InUrl);   // the remembered "headers" was ignored
        Assert.Equal(IdoTokenStyle.CredentialsInHeaders, memory.Get($"{Host}/IDORequestService/ido|TRN|jdoe"));   // and left alone
    }

    // ---- remembering what worked ----

    [Fact]
    public async Task The_style_that_worked_is_tried_first_next_time()
    {
        var memory = new IdoTokenStyleMemory();
        var (client, server) = Client(r => r.InHeaders ? Json(Success) : Status(HttpStatusCode.NotFound), memory);

        await Token(client);          // URL form fails, headers work (2 requests)
        server.Requests.Clear();
        await Token(client);          // straight to headers

        var request = Assert.Single(server.Requests);
        Assert.True(request.InHeaders);
    }

    [Fact]
    public async Task A_remembered_style_that_stops_working_falls_back_again()
    {
        var memory = new IdoTokenStyleMemory();
        memory.Remember($"{Host}/IDORequestService/ido|TRN|jdoe", IdoTokenStyle.CredentialsInHeaders);
        var (client, server) = Client(r => r.InHeaders ? Status(HttpStatusCode.NotFound) : Json(Success), memory);

        Assert.True((await Token(client)).Success);
        Assert.True(server.Requests[0].InHeaders);
        Assert.True(server.Requests[1].InUrl);
    }

    [Fact]
    public async Task The_memory_is_per_environment_and_user()
    {
        var memory = new IdoTokenStyleMemory();
        memory.Remember($"{Host}/IDORequestService/ido|TRN|jdoe", IdoTokenStyle.CredentialsInHeaders);
        var (client, server) = Client(_ => Json(Success), memory);

        await Token(client, user: "someone-else");     // different user: no memory, so the default order
        Assert.True(server.Requests[0].InUrl);
    }

    // ---- when both fail ----

    [Fact]
    public async Task When_neither_form_is_answered_the_message_says_what_was_tried_and_leaks_nothing()
    {
        var (client, server) = Client(r => r.InHeaders ? Status(HttpStatusCode.Unauthorized) : Status(HttpStatusCode.NotFound));

        var result = await Token(client, "jdoe", "hunter2-SECRET");

        Assert.False(result.Success);
        Assert.Equal(2, server.Requests.Count);
        Assert.Contains("credentials in the URL: HTTP 404", result.Message);
        Assert.Contains("credentials in headers: HTTP 401", result.Message);
        Assert.DoesNotContain("hunter2-SECRET", result.Message);
        Assert.DoesNotContain("jdoe", result.Message);
    }

    [Fact]
    public async Task A_skipped_header_form_is_explained_in_the_message()
    {
        var (client, _) = Client(_ => Status(HttpStatusCode.NotFound));

        var result = await Token(client, "jdoe", "example-päss");   // non-ASCII: headers are skipped, the URL form 404s

        Assert.False(result.Success);
        Assert.Contains("credentials in headers: skipped", result.Message);
        Assert.Contains("credentials in the URL: HTTP 404", result.Message);
    }
}
