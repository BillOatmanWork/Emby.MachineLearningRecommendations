using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace ML_Recommendations
{
    public class RecomendationPredictionScheuledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IUserManager UserManager { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log { get; }
        private ITaskManager TaskManager { get; }
        private IServerApplicationPaths AppPaths { get; set; }
      
        private IDtoService Dto { get; set; }
        public RecomendationPredictionScheuledTask(ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager, ITaskManager taskManager, IServerApplicationPaths appPaths, IDtoService dto)
        {
            UserManager = userManager;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            AppPaths = appPaths;
            Dto = dto;
            
            //This event will fire when the plugin needs to load Microsoft.ML libraries, which Emby doesn't ship with
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var trainingDataPath = Path.Combine(AppPaths.DataPath, "recommendation-ratings-train.csv");
            var testDataPath     = Path.Combine(AppPaths.DataPath, "recommendation-ratings-test.csv");

            var trainingCsv = new CsvWriter(trainingDataPath);
            var testCsv     = new CsvWriter(testDataPath);

            var users = UserManager.Users;
            for (var i = 0; i <= users.Count() -1; i++)
            {
                var trainingLibraryData = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    User = users[i],
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Movie) }
                });

                var testLibraryData = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    User = users[i],
                    Recursive = true,
                    IncludeItemTypes = new[] { nameof(Movie) }
                });
               
                //trainingCsv.CreateCsvHeader(trainingLibraryData.Items, users[i]);
                //testCsv.CreateCsvHeader(testLibraryData.Items, users[i]);
            }

           
            
            MLContext mlContext = new MLContext();
            
            (IDataView trainingDataView, IDataView testDataView) = LoadData(mlContext);

            ITransformer model = BuildAndTrainModel(mlContext, trainingDataView);

            EvaluateModel(mlContext, testDataView, model);

            SaveModel(mlContext, trainingDataView.Schema, model);

            UseModelForSinglePrediction(mlContext, model);
        }


        /// <summary>
        /// Load any required dependent libraries into the plugin.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            //Don't try and load items that are not in the Microsoft.ML namespace
            if (!(args.Name.Contains(".ML"))) return null;

            //Don't load the assembly twice
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null) return assembly;


            Log.Info($"Load Request {args.Name}");
            var r1 = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(s => s.Contains(args.Name.Split(',')[0]));
            Log.Info($"Loading Assembly {r1}");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(r1))
            {
                byte[] assemblyData = new byte[stream.Length];
                stream.Read(assemblyData, 0, assemblyData.Length);
                return Assembly.Load(assemblyData);
            }

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

        private (IDataView training, IDataView test) LoadData(MLContext mlContext)
        {
            var trainingDataPath = Path.Combine(AppPaths.DataPath, "recommendation-ratings-train.csv");
            var testDataPath     = Path.Combine(AppPaths.DataPath, "recommendation-ratings-test.csv");
            
            
            IDataView trainingDataView = mlContext.Data.LoadFromTextFile<MovieRating>(trainingDataPath, hasHeader: true, separatorChar: ',');
            IDataView testDataView = mlContext.Data.LoadFromTextFile<MovieRating>(testDataPath, hasHeader: true, separatorChar: ',');

            return (trainingDataView, testDataView);
        }

        ITransformer BuildAndTrainModel(MLContext mlContext, IDataView trainingDataView)
        {
            IEstimator<ITransformer> estimator = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "userIdEncoded", inputColumnName: "userId")
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "tmdbIdEncoded", inputColumnName: "tmdbId"));

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "userIdEncoded",
                MatrixRowIndexColumnName = "tmdbIdEncoded",
                LabelColumnName = "Label",
                NumberOfIterations = 20,
                ApproximationRank = 100
            };

            var trainerEstimator = estimator.Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));
            Log.Info("=============== Training the model ===============");
            ITransformer model = trainerEstimator.Fit(trainingDataView);

            return model;
        }

        private void EvaluateModel(MLContext mlContext, IDataView testDataView, ITransformer model)
        {
            Log.Info("=============== Evaluating the model ===============");
            var prediction = model.Transform(testDataView);
            var metrics = mlContext.Regression.Evaluate(prediction, labelColumnName: "Label", scoreColumnName: "Score");
            Log.Info("Root Mean Squared Error : " + metrics.RootMeanSquaredError.ToString());
            Log.Info("RSquared: " + metrics.RSquared.ToString());
        }

        private void SaveModel(MLContext mlContext, DataViewSchema trainingDataViewSchema, ITransformer model)
        {
            var modelPath = Path.Combine(Environment.CurrentDirectory, "Data", "MovieRecommenderModel.zip");

            Log.Info("=============== Saving the model to a file ===============");
            mlContext.Model.Save(model, trainingDataViewSchema, modelPath);
        }

        private void UseModelForSinglePrediction(MLContext mlContext, ITransformer model)
        {
            Log.Info("=============== Making a prediction ===============");
            var predictionEngine = mlContext.Model.CreatePredictionEngine<MovieRating, MovieRatingPrediction>(model);
            var testInput = new MovieRating { userId = 6, tmdbId = 10 };

            var movieRatingPrediction = predictionEngine.Predict(testInput);
            if (Math.Round(movieRatingPrediction.Score, 1) > 3.5)
            {
                Log.Info("Movie " + testInput.tmdbId + " is recommended for user " + testInput.userId);
            }
            else
            {
                Log.Info("Movie " + testInput.tmdbId + " is not recommended for user " + testInput.userId);
            }
        }

        
        public string Name => "ML Recommendation Training";
        public string Key => "ML Recommendation Training";
        public string Description => "Machine Learning to recommend movies from the library";
        public string Category => "Library";
        public bool IsHidden => true;
        public bool IsEnabled => false;
        public bool IsLogged => true;
    }
}
