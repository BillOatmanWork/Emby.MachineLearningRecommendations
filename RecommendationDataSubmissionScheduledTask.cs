using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace ML_Recommendations
{
    public class RecommendationDataSubmissionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IApplicationPaths AppPaths { get; set; }
        private ILibraryManager LibraryManager { get; set; }
        private IUserManager UserManager { get; set; }
        private ILogger Log { get; set; }
        public RecommendationDataSubmissionScheduledTask(IApplicationPaths appPaths, IUserManager userManager, ILibraryManager libraryManager, ILogManager logMan)
        {
            AppPaths = appPaths;
            LibraryManager = libraryManager;
            UserManager = userManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            
            var trainingDataPath = Path.Combine(AppPaths.DataPath, "recommendation-ratings-train.csv");
            var testDataPath     = Path.Combine(AppPaths.DataPath, "recommendation-ratings-test.csv");

            var trainingCsv = new CsvWriter(trainingDataPath);
            //trainingCsv.CreateCsvHeader("userId,tmdbId,rating");

            var testCsv = new CsvWriter(testDataPath);
            //testCsv.CreateCsvHeader("userId,tmdbId,rating");

            var users = UserManager.Users;
            
            for (var i = 0; i <= users.Count() -1; i++)
            {
                var queryResult = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    User = users[i],
                    Recursive = true,
                    IncludeItemTypes = new[] {nameof(Movie)}
                });

                trainingCsv.WriteData(queryResult.Items.ToList(), users[i]);
                
               
                //testCsv.WriteData(testLibraryData.Items, users[i]);
            }
        }

        private void SubmitTrainingData(string trainingDataPath)
        {
            //Submit training data to online provider in the future...
        }
        
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type          = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public string Name => "ML Recommendation Learning Data";
        public string Key => "ML Recommendation Learning Data";
        public string Description => "Submit Machine Learning data to train a neural network to recommend movies from the library.";
        public string Category => "Library";
        public bool IsHidden => false;
        public bool IsEnabled => false;//Plugin.Instance.Configuration.EnableAnonymousTrainingData;
        public bool IsLogged => true;
    }
}
