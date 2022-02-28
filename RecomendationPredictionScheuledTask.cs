using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace ML_Recommendations
{
    public class RecommendationPredictionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IUserManager UserManager { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log { get; }
        private ITaskManager TaskManager { get; }
        private IApplicationHost ApplicationHost { get; }
      
        public RecommendationPredictionScheduledTask(ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager, ITaskManager taskManager, IApplicationHost host)
        {
            UserManager = userManager;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            ApplicationHost = host;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var mlContext = new MLContext();
            (IDataView trainingDataView, IDataView testDataView) = LoadData(mlContext);

            ITransformer model = BuildAndTrainModel(mlContext, trainingDataView);

            EvaluateModel(mlContext, testDataView, model);

            UseModelForSinglePrediction(mlContext, model);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            throw new NotImplementedException();
        }

        private (IDataView training, IDataView test) LoadData(MLContext mlContext)
        {
            var trainingDataPath = Path.Combine(Environment.CurrentDirectory, "Data", "recommendation-ratings-train.csv");
            var testDataPath = Path.Combine(Environment.CurrentDirectory, "Data", "recommendation-ratings-test.csv");

            IDataView trainingDataView = mlContext.Data.LoadFromTextFile<MovieRating>(trainingDataPath, hasHeader: true, separatorChar: ',');
            IDataView testDataView = mlContext.Data.LoadFromTextFile<MovieRating>(testDataPath, hasHeader: true, separatorChar: ',');

            return (trainingDataView, testDataView);
        }

        ITransformer BuildAndTrainModel(MLContext mlContext, IDataView trainingDataView)
        {
            IEstimator<ITransformer> estimator = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "userIdEncoded", inputColumnName: "userId")
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "movieIdEncoded", inputColumnName: "movieId"));

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "userIdEncoded",
                MatrixRowIndexColumnName = "movieIdEncoded",
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

        //This should be in another scheduled task
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

        public string Name => "ML Recommendation Predictions";
        public string Key => "ML Recommendation Predictions";
        public string Description => "Machine learning movie recommendation predictions";
        public string Category => "Library";
        public bool IsHidden => true;
        public bool IsEnabled => false;
        public bool IsLogged => true;
    }
}
