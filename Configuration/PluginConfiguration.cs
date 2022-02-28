using MediaBrowser.Model.Plugins;

namespace MLRecommendations.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableAnonymousTrainingData { get; set; } = false;
    }
}
