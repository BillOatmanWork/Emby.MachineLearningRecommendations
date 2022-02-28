using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MLRecommendations.Configuration;

namespace ML_Recommendations
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }
        
        public static Plugin Instance { get; set; }

        public override string Name => "ML Recommendations";

        public override Guid Id => new Guid("ABDD70A3-B516-4E35-A0D1-C6447BEBB8BA");


        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo()
            {
                Name = "TensorflowRecommendationsConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.TensorflowRecommendationsConfigurationPage.html"
            },
            new PluginPageInfo()
            {
                Name = "TensorflowRecommendationsConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.TensorflowRecommendationsConfigurationPage.js"
            },
            new PluginPageInfo()
            {
                Name = "brain",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.brain.js"
            }
        };

        public Stream GetThumbImage()
        {
            throw new NotImplementedException();
        }

        public ImageFormat ThumbImageFormat { get; }


    }
}
