using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace AtfLocalizationLab.Tests
{

	[TestFixture]
	[Category("ResourceContent")]
	public class LocalizationPackageTests
	{
		private static readonly string WorkspaceRoot = FindWorkspaceRoot();
		private static readonly string PackageRoot = Path.Combine(WorkspaceRoot, "packages",
			"AtfLocalizationLab");

		[Test]
		[Description("Package-level backend strings are owned by the dedicated source-code schema.")]
		public void BackendResources_AreOwnedBySourceCodeSchema()
		{
			// Arrange
			using (JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(PackageRoot, "Schemas",
				"UsrAtfLocalizationLabMessages", "metadata.json")))) {
				XDocument resources = LoadResource("UsrAtfLocalizationLabMessages.SourceCode", "en-US");

				// Act
				string greeting = FindValue(resources, "LocalizableStrings.SharedGreeting.Value");

				// Assert
				JsonElement schema = metadata.RootElement.GetProperty("MetaData").GetProperty("Schema");
				var declarations = schema.GetProperty("B2").EnumerateArray().ToArray();
				var declaredNames = declarations.Select(item => item.GetProperty("A2").GetString()).ToArray();
				declaredNames.Should().Contain(new[] { "SharedGreeting", "DefaultOnly" },
					because: "the normal backend values must be discoverable and editable in the designer");
				foreach (JsonElement item in declarations) {
					item.GetProperty("A3").GetString().Should().Be(schema.GetProperty("UId").GetString(), because: "these declarations originate in this schema");
					item.GetProperty("A4").GetString().Should().Be(schema.GetProperty("UId").GetString(), because: "these declarations are modified in this schema");
					item.GetProperty("A5").GetString().Should().Be(schema.GetProperty("A5").GetString(), because: "these declarations belong to the lab package");
				}
				greeting.Should().Be("Hello from the localization lab",
					because: "package-level backend code needs a concrete localizable value");
			}
		}

		[Test]
		[Description("Freedom UI strings stay with the page that renders them.")]
		public void FreedomUiResources_AreOwnedByPageSchema()
		{
			// Arrange
			string page = File.ReadAllText(Path.Combine(PackageRoot, "Schemas",
				"UsrAtfLocalizationLabPage", "UsrAtfLocalizationLabPage.js"));
			XDocument resources = LoadResource("UsrAtfLocalizationLabPage.ClientUnit", "en-US");

			// Act
			string greeting = FindValue(resources, "LocalizableStrings.PageGreeting.Value");

			// Assert
			page.Should().Contain("$Resources.Strings.PageGreeting",
				because: "Freedom UI must bind to its schema-owned resource");
			File.ReadAllText(Path.Combine(PackageRoot, "Schemas", "UsrAtfLocalizationLabPage", "metadata.json"))
				.Should().Contain("\"A2\": \"PageGreeting\"",
					because: "Creatio needs localizable-value metadata to populate the page resource dictionary");
			greeting.Should().Be("Hello from the Freedom UI page",
				because: "the page resource must remain with the page schema");
		}

		[TestCase("UsrAtfLocalizationLabMessages.SourceCode", "LocalizableStrings.DefaultOnly.Value")]
		[TestCase("UsrAtfLocalizationLabPage.ClientUnit", "LocalizableStrings.PageDefaultOnly.Value")]
		[Description("Default-only values are deliberately absent from the secondary culture.")]
		public void DefaultOnlyValue_IsAbsentFromSpanish(string resourceFolder, string key)
		{
			// Arrange
			XDocument defaultResources = LoadResource(resourceFolder, "en-US");
			XDocument spanishResources = LoadResource(resourceFolder, "es-ES");

			// Act
			string defaultValue = FindValue(defaultResources, key);
			string spanishValue = FindValue(spanishResources, key);

			// Assert
			defaultValue.Should().NotBeNullOrWhiteSpace(
				because: "the fallback source must exist in the default culture");
			spanishValue.Should().BeNull(
				because: "the live test needs to exercise Creatio fallback rather than a translation");
		}

		private static XDocument LoadResource(string folder, string culture)
		{
			string resourcePath = Path.Combine(PackageRoot, "Resources", folder,
				string.Format("resource.{0}.xml", culture));
			XDocument resource = XDocument.Load(resourcePath);
			return resource;
		}

		private static string FindValue(XDocument document, string key)
		{
			XElement item = document.Descendants("Item")
				.SingleOrDefault(element => (string)element.Attribute("Name") == key);
			if (item == null) {
				return null;
			}
			string value = (string)item.Attribute("Value");
			return value;
		}

		private static string FindWorkspaceRoot()
		{
			DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MainSolution.slnx"))) {
				directory = directory.Parent;
			}
			if (directory == null) {
				throw new DirectoryNotFoundException("Clio workspace root was not found.");
			}
			string workspaceRoot = directory.FullName;
			return workspaceRoot;
		}
	}
}
