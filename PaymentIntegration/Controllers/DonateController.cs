using Microsoft.AspNetCore.Mvc;
using PaymentIntegration.Models;
using PaymentIntegration.Repository;
using PayStack.Net;

namespace PaymentIntegration.Controllers
{
    public class DonateController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly AppDbContext context;
        private readonly string token;
        private  PayStackApi payStack { get; set; }
        public DonateController(IConfiguration configuration, AppDbContext context)
        {
            this.configuration = configuration;
            this.context = context;
            token = this.configuration["Payment:PaystackSK"];
            payStack = new PayStackApi(token);
        }
        public IActionResult Index()
        {

            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Index(DonateViewModel donate)
        {

            TransactionInitializeRequest request = new()
            {
                AmountInKobo = donate.amount * 100,
                Email = donate.Email,
                Reference = Generate().ToString(),
                Currency = "NGN",
                CallbackUrl = "https://localhost:7074/donate/verify"
            };

            TransactionInitializeResponse response = payStack.Transactions.Initialize(request);
            if (response.Status)
            {
                var transaction = new TransactionModel()
                {
                    Amount = donate.amount,
                    Email = donate.Email,
                    TrxRef = request.Reference,
                    Name = donate.Name

                };
                await context.Transactions.AddAsync(transaction);
                await context.SaveChangesAsync();   
                return Redirect(response.Data.AuthorizationUrl);
            }
            ViewData["error"] = response.Message;
            return View();
        }
        [HttpGet]
        public IActionResult  Donations()
        {
            var transactions = context.Transactions.Where(x => x.Status == true).ToList();
            return View(transactions);
        }
        [HttpGet]
        public async Task<IActionResult> Verify(string reference)
        {
            TransactionVerifyResponse response = payStack.Transactions.Verify(reference);
            if(response.Data.Status == "success")
            {
                var transaction =  context.Transactions.Where(t => t.TrxRef == reference).FirstOrDefault(); 
                if(transaction != null)
                {
                    transaction.Status = true;
                    context.Transactions.Update(transaction);
                    await context.SaveChangesAsync();
                    return RedirectToAction("Donations");
                }
            }
            ViewData["error"] = response.Data.GatewayResponse;
            return RedirectToAction("index");
            
        }
        public static int Generate()
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            return rand.Next(100000000, 999999999);
        }
   
    }
}
