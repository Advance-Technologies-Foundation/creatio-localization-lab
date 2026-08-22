using System;
using System.Collections.Generic;
using AtfLocalizationLabApp.LocalizableStrings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Terrasoft.Core;
using Terrasoft.Core.Factories;
using App = AtfLocalizationLabApp.AtfLocalizationLabApp;

namespace AtfLocalizationLab.Tests {

	[TestFixture]
	[Category("Implementation")]
	[NonParallelizable]
	public sealed class AtfLocalizationLabAppTests : BaseComposableAppTestFixture {

		[SetUp]
		public void SetUpApplication() {
			ClassFactory.RebindWithFactoryMethod(() => (UserConnection)UserConnection);
			App.InjectedServices = null;
			App.Instance.Reset();
		}

		[TearDown]
		public void TearDownApplication() {
			App.InjectedServices = null;
			App.Instance.Reset();
		}

		[Test]
		[Description("The default composition root exposes the platform connection and localization services.")]
		public void DefaultServices_AreResolvable() {
			// Arrange
			App application = App.Instance;

			// Act
			UserConnection platformConnection = App.UserConnection;
			ILocalizableStringResolver requiredResolver = application
				.GetRequiredService<ILocalizableStringResolver>();
			ILocalizableStringResolver optionalResolver = application
				.GetService<ILocalizableStringResolver>();
			IEnumerable<ILocalizableStringResolver> resolvers = application
				.GetServices<ILocalizableStringResolver>();
			using (IServiceScope scope = application.CreateScope()) {
				scope.ServiceProvider.GetRequiredService<ILocalizationLabService>().Should()
					.BeOfType<LocalizationLabService>(
						because: "application scopes must resolve the package domain service");
			}

			// Assert
			platformConnection.Should().BeSameAs(UserConnection,
				because: "the application must expose Creatio's platform-owned connection");
			requiredResolver.Should().BeOfType<LocalizableStringResolver>(
				because: "the abstraction must resolve to the generated implementation");
			optionalResolver.Should().BeOfType<LocalizableStringResolver>(
				because: "optional resolution must use the configured resolver registration");
			resolvers.Should().ContainSingle(
				because: "the default application has one localizable-string resolver registration");
		}

		[Test]
		[Description("Tests can inject ordinary and keyed services through the application composition seam.")]
		public void InjectedServices_AreResolvable() {
			// Arrange
			App.InjectedServices = new List<Func<IServiceCollection, IServiceCollection>> {
				services => services
					.AddSingleton("ordinary")
					.AddKeyedSingleton("required", "required keyed")
					.AddKeyedSingleton("optional", "optional keyed")
			};
			App application = App.Instance.Reset();

			// Act
			string ordinary = application.GetRequiredService<string>();
			string requiredKeyed = application.GetRequiredKeyedService<string>("required");
			string optionalKeyed = application.GetKeyedService<string>("optional");

			// Assert
			ordinary.Should().Be("ordinary",
				because: "the test injection seam must add ordinary services");
			requiredKeyed.Should().Be("required keyed",
				because: "required keyed resolution must use the requested service key");
			optionalKeyed.Should().Be("optional keyed",
				because: "optional keyed resolution must use the requested service key");
		}
	}
}
