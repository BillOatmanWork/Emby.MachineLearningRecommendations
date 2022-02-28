using Microsoft.ML.Data;

namespace ML_Recommendations
{
    public class MovieRating
    {
        [LoadColumn(0)]
        public float userId;
        [LoadColumn(1)]
        public float tmdbId;
        [LoadColumn(2)]
        public float Label;
    }
}
