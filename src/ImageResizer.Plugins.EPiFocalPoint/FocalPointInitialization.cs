﻿using System;
using System.Linq;

using EPiServer;
using EPiServer.Core;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.Framework.Localization;
using EPiServer.Framework.Localization.XmlResources;
using EPiServer.Logging;

using ImageResizer.Plugins.EPiFocalPoint.Internal.Services;

namespace ImageResizer.Plugins.EPiFocalPoint {
	[InitializableModule, ModuleDependency(typeof(FrameworkInitialization))]
	public class FocalPointInitialization : IInitializableModule {
		private const string LocalizationProviderName = "FocalPointLocalizations";
		private bool eventsAttached;
		private static readonly ILogger Logger = LogManager.GetLogger();
		public void Initialize(InitializationEngine context) {
			InitializeLocalizations(context);
			InitializeEventHooks(context);
		}
		private static void InitializeLocalizations(InitializationEngine context) {
			var localizationService = context.Locate.Advanced.GetInstance<LocalizationService>() as ProviderBasedLocalizationService;
			if(localizationService != null) {
				var localizationProviderInitializer = new EmbeddedXmlLocalizationProviderInitializer();
				var localizationProvider = localizationProviderInitializer.GetInitializedProvider(LocalizationProviderName, typeof(FocalPointInitialization).Assembly);
				localizationService.Providers.Insert(0, localizationProvider);
			}
		}
		private void InitializeEventHooks(InitializationEngine context) {
			if(!eventsAttached) {
				var contentEvents = context.Locate.Advanced.GetInstance<IContentEvents>();
				contentEvents.CreatingContent += SavingImage;
				contentEvents.SavingContent += SavingImage;
				eventsAttached = true;
			}
		}
		private static void SavingImage(object sender, ContentEventArgs e) {
			var focalPointData = e.Content as IFocalPointData;
			if(focalPointData != null) {
				SetDimensions(focalPointData);
			}
		}
		private static void SetDimensions(IFocalPointData focalPointData) {
			if(!focalPointData.IsReadOnly && focalPointData.BinaryData != null) {
				using(var stream = focalPointData.BinaryData.OpenRead()) {
					try {
						var size = ImageDimensionService.GetDimensions(stream);
						if(size.IsValid) {
							if(focalPointData.OriginalHeight != size.Height) {
								Logger.Information($"Setting height for {focalPointData.Name} to {size.Height}.");
								focalPointData.OriginalHeight = size.Height;
							}
							if(focalPointData.OriginalWidth != size.Width) {
								Logger.Information($"Setting width for {focalPointData.Name} to {size.Width}.");
								focalPointData.OriginalWidth = size.Width;
							}
						} else {
							Logger.Information($"Could not read size of {focalPointData.Name}.");
						}
					} catch(Exception ex) {
						Logger.Error($"Could not read size of {focalPointData.Name}, data might be corrupt.", ex);
					}
				}
			}
		}
		public void Uninitialize(InitializationEngine context) {
			UninitializeLocalizations(context);
			UninitializeEventHooks(context);
		}
		private static void UninitializeLocalizations(InitializationEngine context) {
			var localizationService = context.Locate.Advanced.GetInstance<LocalizationService>() as ProviderBasedLocalizationService;
			var localizationProvider = localizationService?.Providers.FirstOrDefault(p => p.Name.Equals(LocalizationProviderName, StringComparison.Ordinal));
			if(localizationProvider != null) {
				localizationService.Providers.Remove(localizationProvider);
			}
		}
		private void UninitializeEventHooks(InitializationEngine context) {
			var contentEvents = context.Locate.Advanced.GetInstance<IContentEvents>();
			contentEvents.CreatingContent -= SavingImage;
			contentEvents.SavingContent -= SavingImage;
			eventsAttached = false;
		}
	}
}