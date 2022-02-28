using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Entities;

namespace ML_Recommendations
{
    public class CsvWriter 
    {
        private string Path { get; set; }
       
        public CsvWriter(string filePath)
        {
            Path = filePath;
        }
        public void CreateCsvHeader(string header)
        {
            using (var sw = new StreamWriter(Path))
            {
                sw.WriteLine(header);
            }
           
        }

        public void WriteData(List<BaseItem> items, User user)
        {
            //We can't use GUID's in the Neural Network. We'll use the user ID, but strip any alphanumeric characters from it.
            string userId = Regex.Replace(user.Id.ToString(), "[^0-9.]", "");
            using (var sw = new StreamWriter(Path, true))
            {
                foreach (var item in items)
                {
                    sw.WriteLine($"{userId},{item.ProviderIds.FirstOrDefault(p => p.Key == ("Tmdb")).Value},{(item.IsFavorite ? 1 : 0)}");
                }
            }

        }

    }
}
