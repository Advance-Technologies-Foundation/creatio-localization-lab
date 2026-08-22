using System;
using System.Collections.Generic;
using AtfLocalizationLabApp.LocalizableStrings;
using Common.Logging;
using Microsoft.Extensions.DependencyInjection;
using Terrasoft.Core;
using Terrasoft.Core.Factories;

namespace AtfLocalizationLabApp {

	/// <summary>
	/// This is a class that provides access to the application services.
	/// This is the MAIN entry point into the AtfLocalizationLabApp package.
	/// </summary>
	public sealed class AtfLocalizationLabApp {

		#region Fields: Private

		private static Lazy<AtfLocalizationLabApp> _instance = new Lazy<AtfLocalizationLabApp>(() => new AtfLocalizationLabApp());
		private readonly Lazy<ServiceProvider> _serviceProvider = new Lazy<ServiceProvider>(Init);

		/// <summary>
		/// Instance of a UserConnection from ClassFactory.
		/// </summary>
		internal static UserConnection UserConnection => ClassFactory.Get<UserConnection>();

		#endregion

		#region Fields: Internal

		internal static IEnumerable<Func<IServiceCollection, IServiceCollection>> InjectedServices;

		#endregion

		#region Properties: Public

		public static AtfLocalizationLabApp Instance => _instance.Value;

		#endregion

		#region Methods: Private

		private static ServiceProvider Init(){

			ServiceCollection serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ILog>(LogManager.GetLogger(Constants.LoggerName));

			// UserConnection is owned by the Creatio platform (the per-request connection).
			// Never register it as a container-managed (scoped/transient) service: the DI container
			// disposes any IDisposable it resolves from a factory when the scope closes, which would
			// tear down the platform connection's DB executors and clear UserConnection.Current
			// mid-request. Expose it through a Func accessor so the container never owns its lifetime.
			serviceCollection.AddTransient<Func<UserConnection>>(sp => () => UserConnection);
			serviceCollection.AddTransient<ILocalizableStringResolver, LocalizableStringResolver>();
			serviceCollection.AddTransient<ILocalizationLabService, LocalizationLabService>();

			if (InjectedServices != null) {
				foreach (Func<IServiceCollection, IServiceCollection> configureServices in InjectedServices) {
					configureServices(serviceCollection);
				}
			}
			ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
			return serviceProvider;
		}

		#endregion

		#region Methods: Internal

		/// <summary>
		/// This method is meant to be used for testing purposes only.
		/// WARNING: Not thread-safe. Should not be used in production.
		/// This allows test bootstrap to reset the instance between tests
		/// </summary>
		/// <returns></returns>
		internal AtfLocalizationLabApp Reset(){
			_serviceProvider.Value.Dispose();
			_instance = new Lazy<AtfLocalizationLabApp>(() => new AtfLocalizationLabApp());
			AtfLocalizationLabApp application = _instance.Value;
			return application;
		}

		#endregion

		#region Methods: Public

		public T GetKeyedService<T>(object serviceKey) {
			T service = _serviceProvider.Value.GetKeyedService<T>(serviceKey);
			return service;
		}

		public T GetRequiredKeyedService<T>(object serviceKey) {
			T service = _serviceProvider.Value.GetRequiredKeyedService<T>(serviceKey);
			return service;
		}

		public T GetRequiredService<T>() {
			T service = _serviceProvider.Value.GetRequiredService<T>();
			return service;
		}

		public T GetService<T>() {
			T service = _serviceProvider.Value.GetService<T>();
			return service;
		}

		public IEnumerable<T> GetServices<T>() {
			IEnumerable<T> services = _serviceProvider.Value.GetServices<T>();
			return services;
		}

		/// <summary>
		/// Creates a new service scope. Caller is responsible for disposing the scope.
		/// Use this for package-owned scoped services. Do NOT use it to obtain UserConnection:
		/// that connection is owned by the platform — resolve Func&lt;UserConnection&gt; instead.
		/// </summary>
		/// <returns>A new service scope that must be disposed.</returns>
		public IServiceScope CreateScope() {
			IServiceScope scope = _serviceProvider.Value.CreateScope();
			return scope;
		}

		#endregion

		// Private constructor to prevent external instantiation
		private AtfLocalizationLabApp() { }
	}
}
