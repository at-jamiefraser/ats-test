using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using System.Text.RegularExpressions;
using BarraExportParser.Converters;
using BarraExportParser.Model;

namespace BarraProcessor
{
    public class BarraHvaRParser : IBarraParser
    {

        public bool Handles(string name)
        {
            return name.Contains("Portfolio VaR Overview");
        }

        public void Parse(string name, IEnumerable<string> input)
        {
            //var input = File.ReadLines(fileName);
            var date = BarraUtilities.ParseDate(name);
            var code = BarraUtilities.ParseCode(name);

            var csv = new CsvReader(new StringReader(string.Join("\r\n", input.SkipWhile(x => !x.StartsWith("VaR Por/VaR Bmk %")).TakeWhile(x => !x.StartsWith("Positions: ")))));
            csv.Configuration.RegisterClassMap(new HvaRMap());
            var records = csv.GetRecords<BarraHvaR>();

            using (var ctx = new BarraoneDataClassesDataContext())
            {
                Console.WriteLine($"Parsing {code}, {date}, {name}");
                var p = ctx.Portfolios.FirstOrDefault(x => x.Name == code && x.Date == date);

                if (p == null)
                {
                    Console.WriteLine($"No Portfolio for {code} on {date}. Skipping file : {name}");
                    return;
                }

                var barraHvaR = records as BarraHvaR[] ?? records.ToArray();
                
                var existingHvaR = ctx.HVaRExposures.Where(x => x.PortfolioId == p.Id);
                ctx.HVaRExposures.DeleteAllOnSubmit(existingHvaR);

                var hvar = new HVaRExposure()
                {
                    BenchmarkVaRPerc = barraHvaR.FirstOrDefault(x => x.Name == "VAR %").BenchmarkValue,
                    PortfolioVaRPerc = barraHvaR.FirstOrDefault(x => x.Name == "VAR %").PortfolioValue,
                    BenchmarkVaRValue = barraHvaR.FirstOrDefault(x => x.Name == "VAR").BenchmarkValue,
                    PortfolioVaRValue = barraHvaR.FirstOrDefault(x => x.Name == "VAR").PortfolioValue,
                    NAV = barraHvaR.FirstOrDefault(x => x.Name == "NAV").PortfolioValue,
                    PortfolioVaRTotalPerc = barraHvaR.FirstOrDefault(x => x.Name == "Total").BenchmarkValue,

                    PortfolioId = p.Id
                };

                ctx.HVaRExposures.InsertOnSubmit(hvar);
                


                ctx.SubmitChanges();

            }

        }


    }
}
