using Microsoft.VisualStudio.TestTools.UnitTesting;
using EComWeb.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EComWeb.Models;
using EComWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;


namespace EComWeb.Controllers.Tests
{
    [TestClass()]
    public class CustomerControllerTests
    {
        private Mock<IUserRepository> _mockUserRepository;
        private Mock<MyContext> _mockContext;
        private Mock<IWebHostEnvironment> _mockEnvironment;
        private Mock<IConfiguration> _mockConfiguration;
        private CustomerController _customerController;

        [TestInitialize]
        public void Setup()
        {
            // Khởi tạo Mock cho IUserRepository
            _mockUserRepository = new Mock<IUserRepository>();

            // Khởi tạo Mock cho DbContextOptions<MyContext>
            var optionsBuilder = new DbContextOptionsBuilder<MyContext>()
                .UseInMemoryDatabase(databaseName: "TestDatabase"); // Sử dụng InMemoryDatabase để giả lập
            var options = optionsBuilder.Options;

            // Khởi tạo Mock cho MyContext với constructor có tham số DbContextOptions<MyContext>
            _mockContext = new Mock<MyContext>(options);

            // Khởi tạo Mock cho IWebHostEnvironment
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            // Thiết lập các thuộc tính thường dùng của IWebHostEnvironment (nếu cần)
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
            _mockEnvironment.Setup(e => e.WebRootPath).Returns("/wwwroot");

            // Khởi tạo Mock cho IConfiguration
            _mockConfiguration = new Mock<IConfiguration>();
            // Thiết lập giá trị giả lập cho IConfiguration (nếu cần)
            _mockConfiguration.Setup(c => c["SomeKey"]).Returns("SomeValue");

            // Khởi tạo CustomerController với tất cả các mock
            _customerController = new CustomerController(
                _mockContext.Object,         // MyContext
                _mockEnvironment.Object,     // IWebHostEnvironment (evn)
                _mockConfiguration.Object,   // IConfiguration
                _mockUserRepository.Object   // IUserRepository
            );

            // Thiết lập HttpContext và giả lập session
            var httpContext = new DefaultHttpContext();
            var sessionData = new Dictionary<string, byte[]>();
            var mockSession = new Mock<ISession>();

            // Giả lập phương thức TryGetValue của ISession để đọc dữ liệu từ session
            mockSession.Setup(s => s.TryGetValue(It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
                       .Returns((string key, out byte[] value) =>
                       {
                           if (sessionData.TryGetValue(key, out var data))
                           {
                               value = data;
                               return true;
                           }
                           value = null;
                           return false;
                       });

            // Giả lập phương thức Set của ISession để lưu dữ liệu vào session
            mockSession.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
                       .Callback<string, byte[]>((key, value) => sessionData[key] = value);

            // Gán session giả lập vào HttpContext và gán HttpContext cho controller
            httpContext.Session = mockSession.Object;
            _customerController.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }
        // Kiểm tra trường hợp người dùng gửi email và mật khẩu rỗng.
        [TestMethod()]
        public void CustomerLogin_EmptyEmailAndPassword_ReturnsViewWithMessage()
        {
            // Act
            var result = _customerController.CustomerLogin("", "") as ViewResult;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Vui lòng nhập email và mật khẩu!", result.ViewData["Message"]);
        }
        //  Kiểm tra trường hợp người dùng nhập email không tồn tại trong hệ thống.
        [TestMethod()]
        public void CustomerLogin_NonExistentAccount_ReturnsViewWithMessage()
        {
            // Arrange
            string customerEmail = "test@example.com";
            string customerPassword = "password";
            _mockUserRepository.Setup(repo => repo.GetByEmail(customerEmail)).Returns((Customer)null);

            // Act
            var result = _customerController.CustomerLogin(customerEmail, customerPassword) as ViewResult;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Tài khoản hoặc mật khẩu không chính xác!", result.ViewData["Message"]);
        }
        // Kiểm tra trường hợp người dùng nhập mật khẩu sai.
        [TestMethod()]
        public void CustomerLogin_IncorrectPassword_ReturnsViewWithMessage()
        {
            // Arrange
            string customerEmail = "test@example.com";
            string customerPassword = "wrongpassword";
            var customer = new Customer
            {
                customer_email = customerEmail,
                customer_password = BCrypt.Net.BCrypt.HashPassword("correctpassword"),
                customer_id = 1
            };
            _mockUserRepository.Setup(repo => repo.GetByEmail(customerEmail)).Returns(customer);

            // Act
            var result = _customerController.CustomerLogin(customerEmail, customerPassword) as ViewResult;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Tài khoản hoặc mật khẩu không chính xác!", result.ViewData["Message"]);
        }
        //  Kiểm tra trường hợp người dùng nhập email và mật khẩu đúng.
        [TestMethod()]
        public void CustomerLogin_ValidCredentials_RedirectsToIndex()
        {
            // Arrange
            string customerEmail = "test@example.com";
            string customerPassword = "correctpassword";
            var customer = new Customer
            {
                customer_email = customerEmail,
                customer_password = BCrypt.Net.BCrypt.HashPassword(customerPassword),
                customer_id = 1
            };
            _mockUserRepository.Setup(repo => repo.GetByEmail(customerEmail)).Returns(customer);

            // Act
            var result = _customerController.CustomerLogin(customerEmail, customerPassword) as RedirectToActionResult;

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("index", result.ActionName);

            // Kiểm tra session với key "customerSession" và giá trị là customer_id
            var sessionValue = _customerController.HttpContext.Session.GetString("customerSession");
            Assert.AreEqual(customer.customer_id.ToString(), sessionValue);
        }
        // Kiểm tra trường hợp gửi customer null hoặc thông tin trống (email/mật khẩu).
        [TestMethod()]
        public void CustomerRegistration_NullOrEmptyCustomer_ReturnsViewWithMessage()
        {
            // Arrange không cần thiết lập dữ liệu giả lập vì không gọi đến repository.
            // Act 
            var result = _customerController.CustomerRegistration(null) as ViewResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("Vui lòng nhập đầy đủ thông tin!", result.ViewData["Message"]);
        }
        //  Kiểm tra trường hợp đăng ký với email đã tồn tại.
        [TestMethod()]
        public void CustomerRegistration_ExistingEmail_ReturnsViewWithMessage()
        {
            // Arrange 
            var customer = new Customer
            {
                customer_email = "test@example.com",
                customer_password = "password123",
                customer_id = 1,
                customer_name = "Test User"
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(true);
            // Act 
            var result = _customerController.CustomerRegistration(customer) as ViewResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("Tài khoản đã tồn tại!", result.ViewData["Message"]);
        }
        //  Kiểm tra trường hợp đăng ký thành công với thông tin hợp lệ.
        [TestMethod()]
        public void CustomerRegistration_ValidCustomer_RedirectsToCustomerLogin()
        {
            // Arrange (Chuẩn bị):
            var customer = new Customer
            {
                customer_email = "newuser@example.com",
                customer_password = "password123",
                customer_id = 2,
                customer_name = "New User"
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(false);
            _mockUserRepository.Setup(repo => repo.Add(customer)).Verifiable();
            _mockUserRepository.Setup(repo => repo.SaveChanges()).Verifiable();
            // Act 
            var result = _customerController.CustomerRegistration(customer) as RedirectToActionResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("customerLogin", result.ActionName);
            _mockUserRepository.Verify(repo => repo.Add(customer), Times.Once());
            _mockUserRepository.Verify(repo => repo.SaveChanges(), Times.Once());
        }
        // Kiểm tra trường hợp đăng ký với mật khẩu quá ngắn (dưới 6 ký tự).
        [TestMethod()]
        public void CustomerRegistration_ShortPassword_ReturnsViewWithMessage()
        {
            // Arrange 
            var customer = new Customer
            {
                customer_email = "shortpass@example.com",
                customer_password = "12", // Mật khẩu quá ngắn
                customer_id = 3,
                customer_name = "Short Pass User"
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(false);
            // Act 
            var result = _customerController.CustomerRegistration(customer) as ViewResult;
            // Assert  
            Assert.IsNotNull(result);
            Assert.AreEqual("Mật khẩu phải có ít nhất 6 ký tự!", result.ViewData["Message"]);
        }
        // Kiểm tra trường hợp đăng ký với email không hợp lệ (định dạng sai).
        [TestMethod()]
        public void CustomerRegistration_InvalidEmailFormat_ReturnsViewWithMessage()
        {
            // Arrange 
            var customer = new Customer
            {
                customer_email = "invalid-email", // Email không hợp lệ
                customer_password = "password123",
                customer_id = 4,
                customer_name = "Invalid Email User"
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(false);
            // Act 
            var result = _customerController.CustomerRegistration(customer) as ViewResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("Email không hợp lệ! Vui lòng nhập email đúng định dạng.", result.ViewData["Message"]);
        }
        // Kiểm tra trường hợp ngoại lệ khi gọi SaveChanges (ví dụ: lỗi database).
        [TestMethod()]
        public void CustomerRegistration_SaveChangesException_ReturnsViewWithMessage()
        {
            // Arrange
            var customer = new Customer
            {
                customer_email = "exception@example.com",
                customer_password = "password123",
                customer_id = 5,
                customer_name = "Exception User"
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(false);
            _mockUserRepository.Setup(repo => repo.Add(customer)).Verifiable();
            _mockUserRepository.Setup(repo => repo.SaveChanges()).Throws(new Exception("Lỗi lưu dữ liệu"));
            // Act 
            var result = _customerController.CustomerRegistration(customer) as ViewResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("Đã xảy ra lỗi khi đăng ký. Vui lòng thử lại!", result.ViewData["Message"]);
        }
        // Kiểm tra trường hợp đăng ký với các thuộc tính khác của Customer không hợp lệ (ví dụ: tên trống).
        [TestMethod()]
        public void CustomerRegistration_InvalidCustomerProperties_ReturnsViewWithMessage()
        {
            // Arrange 
            var customer = new Customer
            {
                customer_email = "testuser@example.com",
                customer_password = "password123",
                customer_id = 6,
                customer_name = "" // Tên khách hàng trống
            };
            _mockUserRepository.Setup(repo => repo.EmailExists(customer.customer_email)).Returns(false);
            // Act 
            var result = _customerController.CustomerRegistration(customer) as ViewResult;
            // Assert 
            Assert.IsNotNull(result);
            Assert.AreEqual("Tên khách hàng không được để trống!", result.ViewData["Message"]);
        }
    }
}