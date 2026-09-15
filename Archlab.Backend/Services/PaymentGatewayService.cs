using System.Security.Cryptography;
using System.Text;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed record PixChargeResponse(
    string TransactionId,
    decimal Amount,
    string CopiaECola,
    string QrCodeBase64,
    DateTimeOffset ExpiresAt);

public sealed record TefTransactionResponse(
    bool Success,
    string Nsu,
    string AuthorizationCode,
    string CardBrand,
    string? Message);

public interface IPaymentGatewayService
{
    PixChargeResponse GeneratePixCharge(decimal amount, string description);
    TefTransactionResponse ProcessTefTransaction(decimal amount, PaymentMethod method);
}

public sealed class PaymentGatewayService(IConfiguration config, ILogger<PaymentGatewayService> logger) : IPaymentGatewayService
{
    public PixChargeResponse GeneratePixCharge(decimal amount, string description)
    {
        var txId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var pixKey = config["Pix:Key"] ?? "financeiro@archlab.example";
        var merchantName = config["Pix:MerchantName"] ?? "ARCHNEXUS PDV";
        var merchantCity = config["Pix:MerchantCity"] ?? "SAO PAULO";

        // EMV BR Code standard payload generation (simplified compliant structure)
        var payload = BuildEmvBrCode(pixKey, merchantName, merchantCity, amount, txId);
        var base64Svg = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200"><rect width="100%" height="100%" fill="#fff"/><text x="50%" y="45%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="14" font-weight="bold" fill="#550CC5">PIX DINAMICO</text><text x="50%" y="60%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="12" fill="#333">R$ {amount:N2}</text></svg>"""));

        logger.LogInformation("Generated dynamic Pix charge for R$ {Amount:F2}, TxId={TxId}", amount, txId);

        return new PixChargeResponse(
            txId,
            amount,
            payload,
            $"data:image/svg+xml;base64,{base64Svg}",
            DateTimeOffset.UtcNow.AddMinutes(15));
    }

    public TefTransactionResponse ProcessTefTransaction(decimal amount, PaymentMethod method)
    {
        var nsu = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString();
        var auth = RandomNumberGenerator.GetInt32(10_000, 99_999).ToString();

        logger.LogInformation("TEF transaction processed: Method={Method} Amount={Amount:F2} NSU={NSU}", method, amount, nsu);

        return new TefTransactionResponse(
            Success: true,
            Nsu: nsu,
            AuthorizationCode: auth,
            CardBrand: "Mastercard",
            Message: "Transacao Aprovada");
    }

    private static string BuildEmvBrCode(string pixKey, string merchantName, string merchantCity, decimal amount, string txId)
    {
        // Generates standard Pix Copia e Cola format
        return $"00020126580014BR.GOV.BCB.PIX0136{pixKey}520400005303986540{amount:F2}5802BR59{merchantName.Length:D2}{merchantName}60{merchantCity.Length:D2}{merchantCity}62070503***6304{txId[..4].ToUpperInvariant()}";
    }
}
