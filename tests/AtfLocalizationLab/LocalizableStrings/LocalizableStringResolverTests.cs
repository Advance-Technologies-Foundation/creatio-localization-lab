using System;
using System.Globalization;
using AtfLocalizationLabApp.LocalizableStrings;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Terrasoft.Common;
using Terrasoft.Core.Configuration;
using Terrasoft.UnitTest;

namespace AtfLocalizationLab.Tests.LocalizableStrings {

	[TestFixture]
	[Category("Implementation")]
	public sealed class LocalizableStringResolverTests : BaseComposableAppTestFixture {

		private const string ResourceSchemaName = "AtfLocalizationLabMessages";
		private const string ResourceItemName = "LocalizableStrings.SharedGreeting.Value";
		private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

		[Test]
		[Description("The resolver requires an accessor for the platform-owned user connection.")]
		public void Constructor_WithoutUserConnectionAccessor_Throws() {
			// Arrange
			Action construct = () => new LocalizableStringResolver(null);

			// Act
			Action act = construct;

			// Assert
			act.Should().Throw<ArgumentNullException>(
				because: "the resolver cannot locate Creatio resource storage without a user connection accessor");
		}

		[Test]
		[Description("Current-culture resolution delegates to Creatio fallback behavior.")]
		public void GetValue_ReturnsCurrentCultureValue() {
			// Arrange
			IResourceManager resourceManager = ArrangeResourceStorage();
			CultureInfo expectedCulture = CultureInfo.CurrentCulture;
			resourceManager.GetStringWithCultureFallback(ResourceItemName, expectedCulture)
				.Returns("current culture value");
			LocalizableStringResolver resolver = CreateResolver();

			// Act
			string result = resolver.GetValue(ResourceSchemaName, ResourceItemName);

			// Assert
			result.Should().Be("current culture value",
				because: "GetValue must expose Creatio's current-culture fallback resolution");
			resourceManager.Received(1).GetStringWithCultureFallback(ResourceItemName,
				expectedCulture);
		}

		[Test]
		[Description("Strict culture resolution delegates to the non-fallback Creatio API.")]
		public void GetCultureValue_ReturnsStrictCultureValue() {
			// Arrange
			IResourceManager resourceManager = ArrangeResourceStorage();
			resourceManager.GetString(ResourceItemName, SpanishCulture).Returns("strict value");
			LocalizableStringResolver resolver = CreateResolver();

			// Act
			string result = resolver.GetCultureValue(ResourceSchemaName, ResourceItemName, SpanishCulture);

			// Assert
			result.Should().Be("strict value",
				because: "strict resolution must not silently use a fallback culture");
			resourceManager.Received(1).GetString(ResourceItemName, SpanishCulture);
		}

		[Test]
		[Description("Fallback culture resolution delegates to the Creatio fallback API.")]
		public void GetCultureValueWithFallback_ReturnsFallbackValue() {
			// Arrange
			IResourceManager resourceManager = ArrangeResourceStorage();
			resourceManager.GetStringWithCultureFallback(ResourceItemName, SpanishCulture)
				.Returns("fallback value");
			LocalizableStringResolver resolver = CreateResolver();

			// Act
			string result = resolver.GetCultureValueWithFallback(ResourceSchemaName, ResourceItemName,
				SpanishCulture);

			// Assert
			result.Should().Be("fallback value",
				because: "fallback resolution must use Creatio's culture fallback behavior");
			resourceManager.Received(1).GetStringWithCultureFallback(ResourceItemName, SpanishCulture);
		}

		[Test]
		[Description("Missing resource managers produce an explicit no-value result.")]
		public void GetCultureValue_ReturnsNull_WhenResourceManagerIsMissing() {
			// Arrange
			IResourceStorage resourceStorage = Substitute.For<IResourceStorage>();
			SysWorkspace workspace = UserConnection.SetupSysWorkspace();
			workspace.ResourceStorage = resourceStorage;
			resourceStorage.GetManager(ResourceSchemaName).Returns((IResourceManager)null);
			LocalizableStringResolver resolver = CreateResolver();

			// Act
			string result = resolver.GetCultureValue(ResourceSchemaName, ResourceItemName, SpanishCulture);

			// Assert
			result.Should().BeNull(
				because: "the resolver deliberately disables exceptions for unavailable resource managers");
		}

		private IResourceManager ArrangeResourceStorage() {
			IResourceStorage resourceStorage = Substitute.For<IResourceStorage>();
			IResourceManager resourceManager = Substitute.For<IResourceManager>();
			SysWorkspace workspace = UserConnection.SetupSysWorkspace();
			workspace.ResourceStorage = resourceStorage;
			resourceStorage.GetManager(ResourceSchemaName).Returns(resourceManager);
			return resourceManager;
		}

		private LocalizableStringResolver CreateResolver() {
			LocalizableStringResolver resolver = new LocalizableStringResolver(() => UserConnection);
			return resolver;
		}
	}
}
