using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace AtfLocalizationLab.Tests
{
	[TestFixture]
	[Category("Creatio")]
	public class CreatioLocalizationTests
	{
		private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true
		};

		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.SharedGreeting.Value", "es-ES",
			"Hola desde el laboratorio de localización", "Hola desde el laboratorio de localización")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.DefaultOnly.Value", "es-ES",
			null, "Available only in the default language")]
		[TestCase("UsrAtfLocalizationLabPage", "LocalizableStrings.PageGreeting.Value", "es-ES",
			"Hola desde la página Freedom UI", "Hola desde la página Freedom UI")]
		[TestCase("UsrAtfLocalizationLabPage", "LocalizableStrings.PageDefaultOnly.Value", "es-ES",
			null, "This page text exists only in the default language")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.MissingKey.Value", "es-ES", null, null)]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.RegisteredProbe.Value", "en-US", "registered-en-US-1619", "registered-en-US-1619")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.RegisteredProbe.Value", "es-ES", "registered-es-ES-1619", "registered-es-ES-1619")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.XmlOnlyProbe.Value", "en-US", "xml-only-en-US-1619", "xml-only-en-US-1619")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.XmlOnlyProbe.Value", "es-ES", "xml-only-es-ES-1619", "xml-only-es-ES-1619")]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.MetadataOnlyProbe.Value", "en-US", null, null)]
		[TestCase("UsrAtfLocalizationLabMessages", "LocalizableStrings.MetadataOnlyProbe.Value", "es-ES", null, null)]
		[Description("A deployed package exposes Creatio strict and fallback localization behavior.")]
		public void Resolve_ReturnsExpectedStrictAndFallbackValues(string schemaName, string itemName,
			string cultureName, string expectedStrict, string expectedFallback)
		{
			// Arrange
			string environmentName = Environment.GetEnvironmentVariable("CLIO_ENVIRONMENT");
			if (string.IsNullOrWhiteSpace(environmentName)) {
				Assert.Ignore("Set CLIO_ENVIRONMENT to run Creatio-backed localization tests.");
			}
			string destination = Path.Combine(Path.GetTempPath(),
				string.Format("localization-{0:N}.json", Guid.NewGuid()));
			string body = JsonSerializer.Serialize(new {
				resourceSchemaName = schemaName,
				resourceItemName = itemName,
				cultureName
			});

			// Act
			ProcessResult process = RunClio("call-service", "-e", environmentName, "-m", "POST",
				"--service-path", "/rest/AtfLocalizationLabService/Resolve", "-b", body, "-d", destination);
			process.ExitCode.Should().Be(0, because: "the deployed endpoint must succeed before its response is parsed: {0}", process.Output);
			Resolution resolution = null;
			if (File.Exists(destination)) {
				resolution = JsonSerializer.Deserialize<Resolution>(File.ReadAllText(destination), JsonOptions);
			}

			// Assert
			process.ExitCode.Should().Be(0, because: "clio must call the deployed lab endpoint: {0}",
				process.Output);
			resolution.Should().NotBeNull(because: "the endpoint must return a JSON resolution result");
			resolution.CurrentCultureValue.Should().Be(expectedFallback,
				because: "the Value path must resolve under the requested execution culture with fallback");
			resolution.StrictValue.Should().Be(expectedStrict,
				because: "strict lookup must not silently fall back");
			resolution.FallbackValue.Should().Be(expectedFallback,
				because: "fallback lookup must use Creatio's configured fallback behavior");
		}

		[Test]
		[Description("Source-code designer discovery lists B2 declarations, independently of backend XML resource lookup.")]
		public void GetSchema_ShouldExposeOnlyDeclaredStrings_WhenResourcesContainUndeclaredKeys()
		{
			// Arrange
			string environmentName = Environment.GetEnvironmentVariable("CLIO_ENVIRONMENT");
			if (string.IsNullOrWhiteSpace(environmentName)) {
				Assert.Ignore("Set CLIO_ENVIRONMENT to run Creatio-backed localization tests.");
			}
			string destination = Path.Combine(Path.GetTempPath(), "localization-designer-" + Guid.NewGuid().ToString("N") + ".json");
			try {
				// Act
				ProcessResult process = RunClio("call-service", "-e", environmentName, "-m", "POST",
					"--service-path", "ServiceModel/SourceCodeSchemaDesignerService.svc/GetSchema",
					"-b", "{\"schemaUId\":\"0e340e9b-6657-44da-8f3a-a29ea6344519\"}", "-d", destination);
				// Assert
				process.ExitCode.Should().Be(0, because: "the platform designer must return the deployed schema: {0}", process.Output);
				using (JsonDocument response = JsonDocument.Parse(File.ReadAllText(destination))) {
					JsonElement strings = response.RootElement.GetProperty("schema").GetProperty("localizableStrings");
					strings.GetArrayLength().Should().Be(4, because: "normal backend strings and the two declared diagnostic entries belong to the designer collection");
					foreach (JsonElement item in strings.EnumerateArray()) {
						string name = item.GetProperty("name").GetString();
						name.Should().BeOneOf(new[] { "SharedGreeting", "DefaultOnly", "RegisteredProbe", "MetadataOnlyProbe" }, because: "normal strings must be discoverable and XML-only diagnostics are not declarations");
						int expectedCount = 2;
						if (name == "MetadataOnlyProbe") {
							expectedCount = 0;
						}
						else if (name == "DefaultOnly") {
							expectedCount = 1;
						}
						item.GetProperty("values").GetArrayLength().Should().Be(expectedCount,
							because: "B2 declares the item while resource files supply its translated text");
					}
				}
			}
			finally {
				File.Delete(destination);
			}
		}

		[Test]
		[NonParallelizable]
		[Description("The native source-code designer save preserves registered backend translations and item identities.")]
		public void SaveSchema_ShouldPreserveRegisteredTranslations_WhenSavingLoadedSchema()
		{
			// Arrange
			string environmentName = Environment.GetEnvironmentVariable("CLIO_ENVIRONMENT");
			if (string.IsNullOrWhiteSpace(environmentName)) {
				Assert.Ignore("Set CLIO_ENVIRONMENT to an exclusive, writable lab to run Creatio-backed tests.");
			}
			string destination = Path.Combine(Path.GetTempPath(), "localization-save-" + Guid.NewGuid().ToString("N") + ".json");
			try {
				ProcessResult load = RunClio("call-service", "-e", environmentName, "-m", "POST", "--service-path",
					"ServiceModel/SourceCodeSchemaDesignerService.svc/GetSchema", "-b",
					"{\"schemaUId\":\"0e340e9b-6657-44da-8f3a-a29ea6344519\"}", "-d", destination);
				load.ExitCode.Should().Be(0, because: "the round trip needs a successfully loaded native schema: {0}", load.Output);
				using (JsonDocument before = JsonDocument.Parse(File.ReadAllText(destination))) {
					JsonElement schema = before.RootElement.GetProperty("schema");
					schema.GetProperty("isReadOnly").GetBoolean().Should().BeFalse(because: "the disposable lab package must be unlocked for a real designer save");
					// Act
					ProcessResult save = RunClio("call-service", "-e", environmentName, "-m", "POST", "--service-path",
						"ServiceModel/SourceCodeSchemaDesignerService.svc/SaveSchema", "-b", schema.GetRawText(), "-d", destination);
					// Assert
					save.ExitCode.Should().Be(0, because: "the native designer must accept the save: {0}", save.Output);
					using (JsonDocument saved = JsonDocument.Parse(File.ReadAllText(destination))) {
						saved.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(because: "HTTP success alone does not prove a schema was saved");
					}
					ProcessResult reload = RunClio("call-service", "-e", environmentName, "-m", "POST", "--service-path",
						"ServiceModel/SourceCodeSchemaDesignerService.svc/GetSchema", "-b",
						"{\"schemaUId\":\"0e340e9b-6657-44da-8f3a-a29ea6344519\"}", "-d", destination);
					reload.ExitCode.Should().Be(0, because: "the saved schema must be independently reloaded: {0}", reload.Output);
					using (JsonDocument after = JsonDocument.Parse(File.ReadAllText(destination))) {
						after.RootElement.GetProperty("schema").GetProperty("localizableStrings").GetRawText()
							.Should().Be(schema.GetProperty("localizableStrings").GetRawText(), because: "saving must preserve declaration identities, both translations, and default-only omissions");
					}
				}
			}
			finally {
				File.Delete(destination);
			}
		}

		[Test]
		[Description("The deployed Freedom UI page exposes both cultures through Creatio's merged page bundle.")]
		public void FreedomUiPage_ExposesLocalizedResourcesInMergedBundle()
		{
			// Arrange
			string environmentName = Environment.GetEnvironmentVariable("CLIO_ENVIRONMENT");
			if (string.IsNullOrWhiteSpace(environmentName)) {
				Assert.Ignore("Set CLIO_ENVIRONMENT to run Creatio-backed localization tests.");
			}
			string outputDirectory = Path.Combine(Path.GetTempPath(),
				string.Format("localization-page-{0:N}", Guid.NewGuid()));
			string bundlePath = Path.Combine(outputDirectory, ".clio-pages", "UsrAtfLocalizationLabPage",
				"bundle.json");

			try {
				// Act
				ProcessResult process = RunClio("get-page", "--schema-name", "UsrAtfLocalizationLabPage",
					"--output-directory", outputDirectory, "-e", environmentName);

				// Assert
				process.ExitCode.Should().Be(0,
					because: "clio get-page must resolve the deployed Freedom UI schema: {0}", process.Output);
				File.Exists(bundlePath).Should().BeTrue(
					because: "get-page must persist the merged runtime bundle used as the platform oracle");
				using (JsonDocument bundle = JsonDocument.Parse(File.ReadAllBytes(bundlePath))) {
					JsonElement strings = bundle.RootElement.GetProperty("resources").GetProperty("strings");
					strings.GetProperty("PageGreeting").GetProperty("en-US").GetString()
						.Should().Be("Hello from the Freedom UI page",
							because: "the default page resource must be registered in the runtime bundle");
					strings.GetProperty("PageGreeting").GetProperty("es-ES").GetString()
						.Should().Be("Hola desde la página Freedom UI",
							because: "the secondary page translation must be registered in the runtime bundle");
					strings.GetProperty("PageDefaultOnly").TryGetProperty("es-ES", out _)
						.Should().BeFalse(
							because: "the secondary-culture page must exercise default-language fallback");
				}
			}
			finally {
				if (Directory.Exists(outputDirectory)) {
					Directory.Delete(outputDirectory, true);
				}
			}
		}

		private static ProcessResult RunClio(params string[] arguments)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo("clio") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			foreach (string argument in arguments) {
				startInfo.ArgumentList.Add(argument);
			}
			using (Process process = Process.Start(startInfo)) {
				if (process == null) {
					throw new InvalidOperationException("clio did not start.");
				}
				string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
				process.WaitForExit();
				return new ProcessResult(process.ExitCode, output);
			}
		}

		private sealed class ProcessResult
		{
			public ProcessResult(int exitCode, string output)
			{
				ExitCode = exitCode;
				Output = output;
			}

			public int ExitCode { get; private set; }
			public string Output { get; private set; }
		}

		private sealed class Resolution
		{
			public string CurrentCultureValue { get; set; }
			public string StrictValue { get; set; }
			public string FallbackValue { get; set; }
		}
	}
}
