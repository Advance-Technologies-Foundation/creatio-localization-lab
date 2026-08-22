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

		[TestCase("AtfLocalizationLabMessages", "LocalizableStrings.SharedGreeting.Value", "es-ES",
			"Hola desde el laboratorio de localización", "Hola desde el laboratorio de localización")]
		[TestCase("AtfLocalizationLabMessages", "LocalizableStrings.DefaultOnly.Value", "es-ES",
			null, "Available only in the default language")]
		[TestCase("UsrAtfLocalizationLabPage", "LocalizableStrings.PageGreeting.Value", "es-ES",
			"Hola desde la página Freedom UI", "Hola desde la página Freedom UI")]
		[TestCase("UsrAtfLocalizationLabPage", "LocalizableStrings.PageDefaultOnly.Value", "es-ES",
			null, "This page text exists only in the default language")]
		[TestCase("AtfLocalizationLabMessages", "LocalizableStrings.MissingKey.Value", "es-ES", null, null)]
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
