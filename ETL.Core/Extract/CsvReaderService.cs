using CsvHelper;
using System.Globalization;

namespace ETL.Core.Extract
{
    public class CsvReaderService
    {
        public List<T> Leer<T>(string rutaArchivo)
        {
            using var reader = new StreamReader(rutaArchivo);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<T>().ToList();
        }
    }
}