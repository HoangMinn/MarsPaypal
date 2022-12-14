using Mars_Store.fonts.Models.Function;
using Mars_Store.Models.Entities;
using Mars_Store.Models.Function;
using Mars_Store.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PayPal.Api;
using Mars_Store.Models;

namespace Mars_Store.Controllers
{
    public class CartController : Controller
    {
        //
        // GET: /Cart/
        private const string CartSession = "CartSession";
        public ActionResult Index()
        {
            var cart = (Cart)Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = cart.Lines.ToList();
                ViewBag.TongTien = cart.ComputeTotalValue();
                ViewBag.TotalItem = cart.TotalItem();
            }
            return View(list);
        }
        public ActionResult AddItem(int Id)
        {

            var product = new SANPHAMFunction().FindEntity(Id); ;
            var cart = (Cart)Session[CartSession];
            if (cart != null)
            {
                cart.AddItem(product, 1);
                //Gán vào session
                Session[CartSession] = cart;
            }
            else
            {
                //tạo mới đối tượng cart item
                cart = new Cart();
                cart.AddItem(product, 1);
                //Gán vào session
                Session[CartSession] = cart;
            }
            return RedirectToAction("Index");
        }

        public RedirectToRouteResult AddToCart(int id)
        {
            var product = new SANPHAMFunction().FindEntity(id); ;
            var cart = (Cart)Session[CartSession];
            if (cart != null)
            {
                cart.AddItem(product, 1);
                //Gán vào session
                Session[CartSession] = cart;
            }
            else
            {
                //tạo mới đối tượng cart item
                cart = new Cart();
                cart.AddItem(product, 1);
                //Gán vào session
                Session[CartSession] = cart;
            }
            return RedirectToAction("Index", "Cart");
        }
        public ActionResult RemoveOneItem(int Id)
        {

            var product = new SANPHAMFunction().FindEntity(Id); ;
            var cart = (Cart)Session[CartSession];
            if (cart != null)
            {
                cart.AddItem(product, -1);
                //Gán vào session
                Session[CartSession] = cart;
            }
            return RedirectToAction("Index", "Cart");
        }

        public ActionResult Clear()
        {
            var cart = (Cart)Session[CartSession];
            cart.Clear();
            Session[CartSession] = cart;
            return RedirectToAction("Index", "Cart");
        }
        public ActionResult RemoveLine(int Id)
        {

            var product = new SANPHAMFunction().FindEntity(Id); ;
            var cart = (Cart)Session[CartSession];
            if (cart != null)
            {
                cart.RemoveLine(product);
                //Gán vào session
                Session[CartSession] = cart;
            }
            return RedirectToAction("Index");
        }
        public ActionResult Payment(string name, string mobileadd, string diachiadd, string dateout)
        {
            // A
            var order = new DONHANG();
            order.ngaylap = DateTime.Now;
            order.hotenkh = name;
            order.diachigiaohang = diachiadd;
            order.phone = mobileadd;
            DateTime? date = null;
            DateTime temp;

            if (DateTime.TryParse(dateout, out temp))
            {
                if (temp != null)
                    date = temp;
            }

            if (date != null)
                order.ngaynhanhang = date.Value;

            // B

            //nếu login
            if (SessionPersister.UserName != null){
                order.ngaynhanhang = DateTime.Now;
                order.ID_TK = SessionPersister.UserName.ID_TK;

                var account = new TAIKHOANFunction.TaiKhoanFunction().FindEntity(order.ID_TK.Value);
                order.hotenkh = account.tentk;
                order.diachigiaohang = account.diachi;
                order.phone = account.phone;
                }
            try
            {
                var id = new DONHANGFunction.DonHangFunction().Insert(order);
                
                var cart = (Cart)Session["CartSession"];
                var detailDao = new CTDONHANGFunction.CTDonHangFunction();
                foreach (var item in cart.Lines)
                {
                    var orderDetail = new CTDONHANG();
                    orderDetail.ID_SP = item.Sanpham.ID_SP;
                    orderDetail.ID_DH = id;
                    orderDetail.soluong = item.Quantity;
                    orderDetail.dongia = (item.Sanpham.giabd * item.Quantity);
                    detailDao.Insert(orderDetail);
                }

                Session["CartSession"] = null;
            }
            catch (Exception ex)
            {
                //ghi log
                return RedirectToAction("Loi"); // action Loi ở đâu?
            }

            return RedirectToAction("MuaHangThanhCong", "Cart");
        }

        public ActionResult PaymentNotLogin()
        {
            var cart = (Cart)Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = cart.Lines.ToList();
                ViewBag.TongTien = cart.ComputeTotalValue();
                ViewBag.TotalItem = cart.TotalItem();
            }

            return View(list);
        }
        public ActionResult MuaHangThanhCong()
        {
            return View();
        }
        public PartialViewResult ShowInfor()
        {
            MyDB context = new MyDB();
            var user = (from a in context.TAIKHOANs
                        where (a.username == SessionPersister.UserName.UserName)
                        select a).FirstOrDefault();
            return PartialView("ShowInfor", user);
        }

        public ActionResult ConfirmInfo()
        {
            if (SessionPersister.UserName == null)
            {
                return RedirectToAction("Index", "UserLogin");
            }

            var cart = (Cart)Session[CartSession];
            var list = new List<CartItem>();
            if (cart != null)
            {
                list = cart.Lines.ToList();
                ViewBag.TongTien = cart.ComputeTotalValue();
                ViewBag.TotalItem = cart.TotalItem();
            }
            return View(list);
        }

        //PayPal
        private Payment payment;

        private Payment CreatePayment(APIContext apiContext, string redirectUrl)
        {
            var listItems = new ItemList() { items = new List<Item>() };
            
            var  listCarts = new List<CartItem>();
            foreach (var cart in listCarts)
            {
                listItems.items.Add(new Item()
                {
                    name = cart.Sanpham.tensanpham,
                    currency = "USD",
                    price = cart.Sanpham.giabd.ToString(),
                    quantity = cart.Quantity.ToString(),
                    sku = "sku"
                });
            }

            var payer = new Payer() { payment_method = "paypal" };

            var redirUrls = new RedirectUrls()
            {
                cancel_url = redirectUrl,
                return_url = redirectUrl
            };

            var details = new Details()
            {
                tax = "1",
                shipping = "2",
                subtotal = listCarts.Sum(e => e.Sanpham.giabd * e.Quantity).ToString()
            };

            var amount = new Amount()
            {
                currency = "USD",
                total = (Convert.ToDouble(details.tax) + Convert.ToDouble(details.shipping) + Convert.ToDouble(details.subtotal)).ToString(),
                details = details            
            };

            var transactionList = new List<Transaction>();
            transactionList.Add(new Transaction()
            {
                description = "Minh testing",
                invoice_number = Convert.ToString((new Random()).Next(1000000)),
                amount = amount,
                item_list = listItems
            });

            payment = new Payment()
            {
                intent = "sale",
                payer = payer,
                transactions = transactionList,
                redirect_urls = redirUrls
            };

            return payment.Create(apiContext);
        }

        private Payment ExecutePayment(APIContext apiContext, string payerId, string paymentId)
        {
            var paymentExecution = new PaymentExecution()
            {
                payer_id = payerId
            };
            payment = new Payment() { id = paymentId };
            return payment.Execute(apiContext, paymentExecution);
        }

        public ActionResult PaymentWithPaypal()
        {
            APIContext apiContext = PaypalConfiguration.GetAPIContext();

            try
            {
                string payerId = Request.Params["PayerID"];
                if (string.IsNullOrEmpty(payerId))
                {
                    string baseURI = Request.Url.Scheme + "://" + Request.Url.Authority + "Cart/PaymentWithPaypal?";
                    var guid = Convert.ToString((new Random()).Next(1000000));
                    var createdPayment = CreatePayment(apiContext, baseURI + "guid=" + guid);

                    var links = createdPayment.links.GetEnumerator();
                    string paypalRedirectUrl = string.Empty;

                    while(links.MoveNext())
                    {
                        Links link = links.Current;
                        if (link.rel.ToLower().Trim().Equals("approval_url"))
                        {
                            paypalRedirectUrl = link.href;
                        }
                    }

                    Session.Add(guid, createdPayment.id);
                    return Redirect(paypalRedirectUrl);
                }
                else
                {
                    var guid = Request.Params["guid"];
                    var executedPayment = ExecutePayment(apiContext, payerId, Session[guid] as string);
                    if (executedPayment.state.ToLower() != "approved")
                    {
                        return View("Failure");
                    }
                }
            }catch(Exception ex)
            {
                PaypalLogger.Log("Error: " + ex.Message);
                return View("Failure");
            }

            return View("Success");
        }
    }
}