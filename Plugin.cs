// ВНИМАНИЕ: Данный плагин не рекомендуется использовать при работе с ФФД 1.1 и выше

using RetailCorrector.Plugin;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Nodes;
using Validators;

[assembly: Guid("013dc59c-657b-4364-8cd9-68ba9cd7b3be")]

namespace FirstOfd
{
    public class Plugin : SourcePlugin
    {
        public override string Name => "1-ОФД";

        [DisplayName("Ключ API")]
        public string Token { get; set; }

        [DisplayName("ИНН организации")]
        public string Vatin
        {
            get => _vatin ?? "";
            set
            {
                if (VatinValidator.Valid(value)) _vatin = value;
                else Notify("Введен некорректный ИНН...", "Некорректные данные");
            }
        }
        private string? _vatin = "";

        [DisplayName("Регистрационный номер")]
        public string DeviceId
        {
            get => _regId ?? "";
            set
            {
                if (DeviceValidator.Valid(value)) _regId = value;
                else Notify("Введен некорректный регистрационный номер...", "Некорректные данные");
            }
        }
        private string? _regId = "";

        [DisplayName("Заводской номер накопителя")]
        public string StorageId { get; set; }

        [DisplayName("Начало сканирования")]
        public DateOnly Started { get; set; }

        [DisplayName("Конец сканирования")]
        public DateOnly Ended { get; set; }

        private HttpClient? http;

        public override Task OnLoad(AssemblyLoadContext ctx)
        {
            http = new HttpClient { BaseAddress = new Uri(Constants.BaseUrl) };
            return Task.CompletedTask;
        }

        public override Task OnUnload()
        {
            http?.Dispose();
            http = null;
            return Task.CompletedTask;
        }

        public override async Task<IEnumerable<Receipt>> Parse(CancellationToken token)
        {
            var tempKey = await Auth();
            if (tempKey is null)
            {
                Notify("Не удалось получить временный токен!");
                return [];
            }
            List<Receipt> receipts = [];
            for (var day = Started; day <= Ended; day = day.AddDays(1))
            {
                receipts.AddRange(await Parse(day, tempKey, token));
                await Task.Delay(1000, token);
            }
            return receipts;
        }

        private async Task<string?> Auth()
        {
            try
            {
                using var resp = await http!.PostAsync("/api/auth",
                    new StringContent($"{{\"apiKey\":\"{Token}\"}}",
                    MediaTypeHeaderValue.Parse("application/json")));
                var content = await resp.Content.ReadAsStringAsync();
                return JsonNode.Parse(content)!["token"]!.GetValue<string>();
            }
            catch(Exception e)
            {
                Log("Не удалось получить временный токен...", true, e);
                return null;
            }
        }

        private async Task<Receipt[]> Parse(DateOnly day, string key, CancellationToken token)
        {
            var builder = new StringBuilder();
            builder.Append($"/api/rent/v2/organisations/{Vatin}/documents");
            builder.Append($"?kkmRegId={DeviceId}&transactionTypes=TICKET");
            builder.Append($"&fsFactoryNumber={StorageId}");
            builder.Append($"&fromDate={day:yyyy'-'MM'-'dd}T00:00:00");
            builder.Append($"&toDate={day:yyyy'-'MM'-'dd}T23:59:59");

            using var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, builder.ToString());
            req.Headers.Add("Authorization", $"Bearer {key}");
            using var resp = await http!.SendAsync(req, token);
            var content = await resp.Content.ReadAsStringAsync(token);
            var json = JsonNode.Parse(content)!["documents"]!.AsArray();
            var receipts = new Receipt[json.Count];
            for(var i = 0; i < json.Count; i++)
            {
                var ticket = json[i]!["ticket"]!;
                var items = ticket["items"]!.AsArray()!;
                receipts[i] = new Receipt
                {
                    FiscalSign = json[i]!["fiscalSign"]!.GetValue<string>(),
                    Operation = (Operation)ticket["operationType"]!.GetValue<int>(),
                    Items = new Position[items.Count],
                    TotalSum = (uint)(ticket["totalSum"]!.GetValue<double>() * 100),
                    Payment = new Payment
                    {
                        Cash = (uint)(ticket["cashTotalSum"]!.GetValue<double>() * 100),
                        ECash = (uint)(ticket["ecashTotalSum"]!.GetValue<double>() * 100),
                        Pre = (uint)(ticket["prepaymentSum"]!.GetValue<double>() * 100),
                        Post = (uint)(ticket["postpaymentSum"]!.GetValue<double>() * 100),
                    },
                    Created = DateTime.ParseExact(json[i]!["transactionDate"]!.GetValue<string>(), "yyyy'-'MM'-'dd'T'HH':'mm':'ss", null)
                };
                for (var j = 0; j < receipts[i].Items.Length; j++)
                    receipts[i].Items[j] = new Position
                    {
                        Name = items[j]!["name"]!.GetValue<string>(),
                        Price = (uint)(items[j]!["price"]!.GetValue<double>() * 100),
                        Quantity = (uint)(items[j]!["quantity"]!.GetValue<double>() * 1000),
                        TotalSum = (uint)(items[j]!["sum"]!.GetValue<double>() * 100),
                        PayType = (PaymentType)items[j]!["calculationTypeSign"]!.GetValue<int>(),
                        PosType = (PositionType)items[j]!["calculationSubjectSign"]!.GetValue<int>(),
                        TaxRate = (TaxRate)items[j]!["ndsRate"]!.GetValue<int>(),
                    };
            }
            return receipts;
        }
    }
}
