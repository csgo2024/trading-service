using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Trading.Common.Helpers;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Abstraction.Contracts;
using Xunit;

namespace Trading.Infrastructure.Tests;

public class ApiCredentialProviderTests
{
    private const string TestApiKey = "test_api_key";
    private const string TestApiSecret = "test_api_secret";

    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ICredentialSettingRepository> _mockRepository;
    private readonly Mock<IOptions<CredentialSettingV2>> _mockOptions;
    private readonly ApiCredentialProvider _provider;

    public ApiCredentialProviderTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockRepository = new Mock<ICredentialSettingRepository>();
        _mockOptions = new Mock<IOptions<CredentialSettingV2>>();

        _provider = new ApiCredentialProvider(
            _mockOptions.Object,
            _mockConfiguration.Object,
            _mockRepository.Object
        );
    }

    private static (byte[] apiKeyBytes, byte[] apiSecretBytes, string privateKey) GetEncryptedTestDataBytes()
    {
        return RsaEncryptionHelper.EncryptApiCredentialToBytes(TestApiKey, TestApiSecret);
    }

    private static (string apiKeyBase64, string apiSecretBase64, string privateKey) GetEncryptedTestDataBase64()
    {
        return RsaEncryptionHelper.EncryptApiCredentialToBase64(TestApiKey, TestApiSecret);
    }

    [Fact]
    public void GetBinanceSettingsV1_WithValidData_ReturnsDecryptedSettings()
    {
        // Arrange
        var (apiKeyBytes, apiSecretBytes, privateKey) = GetEncryptedTestDataBytes();
        var settings = new CredentialSetting { ApiKey = apiKeyBytes, ApiSecret = apiSecretBytes };

        _mockConfiguration.Setup(x => x.GetSection("PrivateKey").Value).Returns(privateKey);
        _mockRepository.Setup(x => x.GetEncryptedRawSetting()).Returns(settings);

        // Act
        var result = _provider.GetCredentialSettingsV1();

        // Assert
        Assert.Equal(TestApiKey, result.ApiKey);
        Assert.Equal(TestApiSecret, result.ApiSecret);
    }

    [Fact]
    public void GetBinanceSettingsV1_WithNullSettings_ShouldNot_ReturnsEmptySettings()
    {
        // Arrange
        _mockConfiguration.Setup(x => x.GetSection("PrivateKey").Value).Returns(string.Empty);
        _mockRepository.Setup(x => x.GetEncryptedRawSetting()).Returns(null as CredentialSetting);

        // Act
        var result = _provider.GetCredentialSettingsV1();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.ApiKey);
        Assert.NotEmpty(result.ApiSecret);
    }

    [Fact]
    public void GetBinanceSettingsV2_WithValidData_ReturnsDecryptedSettings()
    {
        // Arrange
        var (apiKeyBase64, apiSecretBase64, privateKey) = GetEncryptedTestDataBase64();

        _mockOptions.Setup(x => x.Value).Returns(new CredentialSettingV2
        {
            ApiKey = apiKeyBase64,
            ApiSecret = apiSecretBase64,
            PrivateKey = privateKey
        });

        // Act
        var result = _provider.GetCredentialSettingsV2();

        // Assert
        Assert.Equal(TestApiKey, result.ApiKey);
        Assert.Equal(TestApiSecret, result.ApiSecret);
    }
}
