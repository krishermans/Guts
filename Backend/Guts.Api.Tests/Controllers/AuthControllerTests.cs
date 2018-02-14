using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using Guts.Api.Controllers;
using Guts.Api.Tests.Builders;
using Guts.Business.Captcha;
using Guts.Business.Communication;
using Guts.Business.Security;
using Guts.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Guts.Api.Tests.Controllers
{
    [TestFixture]
    public class AuthControllerTests
    {
        private AuthController _controller;
        private Mock<UserManager<User>> _userManagerMock;
        private Mock<ICaptchaValidator> _captchaValidatorMock;
        private Mock<IMailSender> _mailSenderMock;
        private Mock<IPasswordHasher<User>> _passwordHasherMock;
        private Mock<ITokenAccessPassFactory> _accessPassFactoryMock;

        [SetUp]
        public void Setup()
        {
            var userStoreMock = new Mock<IUserStore<User>>();
            _passwordHasherMock = new Mock<IPasswordHasher<User>>();
            var lookupNormalizerMock = new Mock<ILookupNormalizer>();
            var errorsMock = new Mock<IdentityErrorDescriber>();
            var loggerMock = new Mock<ILogger<UserManager<User>>>();

            _userManagerMock = new Mock<UserManager<User>>(
                userStoreMock.Object,
                null,
                _passwordHasherMock.Object,
                null,
                null,
                lookupNormalizerMock.Object,
                errorsMock.Object,
                null,
                loggerMock.Object);

            _userManagerMock.Setup(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(new IdentityResult());

            var expectedCaptchaResult = new CaptchaVerificationResult { Success = true };
            _captchaValidatorMock = new Mock<ICaptchaValidator>();
            _captchaValidatorMock.Setup(validator => validator.Validate(It.IsAny<string>(), It.IsAny<IPAddress>()))
                .ReturnsAsync(expectedCaptchaResult);

            _mailSenderMock = new Mock<IMailSender>();

            _accessPassFactoryMock = new Mock<ITokenAccessPassFactory>();

            _controller = new AuthController(_userManagerMock.Object,
                _passwordHasherMock.Object,
                _captchaValidatorMock.Object,
                _mailSenderMock.Object,
               _accessPassFactoryMock.Object);

            var context = new ControllerContextBuilder().WithClientIp().Build();
            _controller.ControllerContext = context;
        }

        [Test]
        public void RegisterShouldReturnBadRequestIfModelIsInvalid()
        {
            //Arrange
            var model = new RegisterModelBuilder().Build();

            var errorKey = "someValidationError";
            _controller.ModelState.AddModelError(errorKey, Guid.NewGuid().ToString());

            //Act
            var result = _controller.Register(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorKey));
            _userManagerMock.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void RegisterShouldReturnBadRequestForNonPxlEmailAdres()
        {
            //Arrange
            var model = new RegisterModelBuilder().WithEmail("invalid@nottrusted.be").Build();

            //Act
            var result = _controller.Register(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value, Does.Contain("PXL").IgnoreCase);
            _userManagerMock.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void RegisterShouldCreateUserAndSendConfirmationMail()
        {
            //Arrange
            var model = new RegisterModelBuilder().WithValidEmail().Build();

            var confirmationToken = Guid.NewGuid().ToString();
            _userManagerMock.Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
                .ReturnsAsync(confirmationToken);

            var identityResult = IdentityResult.Success;
            _userManagerMock.Setup(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(identityResult);

            //Act
            var result = _controller.Register(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            _userManagerMock.Verify(
                manager => manager.CreateAsync(
                    It.Is<User>(user => user.Email == model.Email && user.UserName == model.Email), model.Password),
                Times.Once);

            _userManagerMock.Verify(
                manager => manager.GenerateEmailConfirmationTokenAsync(It.Is<User>(user => user.Email == model.Email)),
                Times.Once);

            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.Is<User>(user => user.Email == model.Email), confirmationToken), Times.Once);
        }

        [Test]
        public void RegisterShouldReturnBadRequestIfCreatingUserFails()
        {
            //Arrange
            var model = new RegisterModelBuilder().WithValidEmail().Build();

            var errorCode = Guid.NewGuid().ToString();
            var identityResult = IdentityResult.Failed(new IdentityError
            {
                Code = errorCode,
                Description = Guid.NewGuid().ToString()
            });

            _userManagerMock.Setup(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(identityResult);

            //Act
            var result = _controller.Register(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorCode));

            _userManagerMock.Verify(
                manager => manager.CreateAsync(
                    It.Is<User>(user => user.Email == model.Email && user.UserName == model.Email), model.Password),
                Times.Once);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void RegisterShouldValidateCaptcha()
        {
            //Arrange
            var model = new RegisterModelBuilder().WithValidEmail().Build();

            //Act
            var result = _controller.Register(model).Result;

            //Assert
            Assert.That(result, Is.Not.Null);
            _captchaValidatorMock.Verify(validator => validator.Validate(model.CaptchaToken, _controller.HttpContext.Connection.RemoteIpAddress));
        }

        [Test]
        public void RegisterShouldReturnBadRequestIfCaptchaIsInvalid()
        {
            //Arrange
            var model = new RegisterModelBuilder().WithValidEmail().Build();
            var expectedCaptchaResult = new CaptchaVerificationResult { Success = false };
            _captchaValidatorMock.Setup(validator => validator.Validate(It.IsAny<string>(), It.IsAny<IPAddress>()))
                .ReturnsAsync(expectedCaptchaResult);

            //Act
            var result = _controller.Register(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _captchaValidatorMock.Verify(validator => validator.Validate(model.CaptchaToken, _controller.HttpContext.Connection.RemoteIpAddress));
            _userManagerMock.Verify(manager => manager.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void CreateTokenShoudReturnBadRequestIfModelIsInvalid()
        {
            //Arrange
            var model = new LoginModelBuilder().Build();

            var errorKey = "someValidationError";
            _controller.ModelState.AddModelError(errorKey, Guid.NewGuid().ToString());

            //Act
            var result = _controller.CreateToken(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorKey));

            _userManagerMock.Verify(manager => manager.FindByNameAsync(It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _passwordHasherMock.Verify(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void CreateTokenShoudReturnUnAuthorizedIfUserIsNotFound()
        {
            //Arrange
            var model = new LoginModelBuilder().Build();
            _userManagerMock.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(() => null);

            //Act
            var result = _controller.CreateToken(model).Result as UnauthorizedResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            _userManagerMock.Verify(manager => manager.FindByNameAsync(model.Email), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _passwordHasherMock.Verify(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void CreateTokenShoudReturnUnAuthorizedIfPasswordsDontMatch()
        {
            //Arrange
            var model = new LoginModelBuilder().Build();

            var existingUser = new User
            {
                PasswordHash = Guid.NewGuid().ToString()
            };
            _userManagerMock.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            _passwordHasherMock
                .Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(PasswordVerificationResult.Failed);

            //Act
            var result = _controller.CreateToken(model).Result as UnauthorizedResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            _userManagerMock.Verify(manager => manager.FindByNameAsync(model.Email), Times.Once);
            _passwordHasherMock.Verify(hasher => hasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, model.Password), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            
        }

        [Test]
        public void CreateTokenShoudReturnBadRequestAndSendConfirmationMailWhenEmailIsNotConfirmed()
        {
            //Arrange
            var model = new LoginModelBuilder().Build();

            var existingUser = new User
            {
                EmailConfirmed = false
            };
            _userManagerMock.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            var confirmationToken = Guid.NewGuid().ToString();
            _userManagerMock.Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
                .ReturnsAsync(confirmationToken);

            _passwordHasherMock
                .Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(PasswordVerificationResult.Success);

            //Act
            var result = _controller.CreateToken(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo("EmailNotConfirmed"));

            _userManagerMock.Verify(manager => manager.FindByNameAsync(model.Email), Times.Once);
            _passwordHasherMock.Verify(hasher => hasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, model.Password), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(existingUser, confirmationToken), Times.Once);
        }

        [Test]
        public void CreateTokenShoudReturnOkResultWithTokenIfCredentialsAreCorrect()
        {
            //Arrange
            var model = new LoginModelBuilder().Build();

            var existingUser = new User
            {
                EmailConfirmed = true
            };
            _userManagerMock.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            var confirmationToken = Guid.NewGuid().ToString();
            _userManagerMock.Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<User>()))
                .ReturnsAsync(confirmationToken);

            _passwordHasherMock
                .Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(PasswordVerificationResult.Success);

            var createdAccesPass = new TokenAccessPass
            {
                Token = Guid.NewGuid().ToString()
            };
            _accessPassFactoryMock
                .Setup(factory => factory.Create(It.IsAny<User>(), It.IsAny<IList<Claim>>()))
                .Returns(createdAccesPass);

            var existingClaims = new List<Claim>();
            _userManagerMock.Setup(manager => manager.GetClaimsAsync(It.IsAny<User>())).ReturnsAsync(existingClaims);

            //Act
            var result = _controller.CreateToken(model).Result as OkObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            _userManagerMock.Verify(manager => manager.FindByNameAsync(model.Email), Times.Once);
            _passwordHasherMock.Verify(hasher => hasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, model.Password), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
            _userManagerMock.Verify(manager => manager.GetClaimsAsync(existingUser), Times.Once);
            _accessPassFactoryMock.Verify(factory => factory.Create(existingUser, existingClaims), Times.Once);

            Assert.That(result.Value, Is.SameAs(createdAccesPass));
        }

        [Test]
        public void ConfirmEmailShouldReturnBadRequestIfModelIsInvalid()
        {
            //Arrange
            var model = new ConfirmEmailModelBuilder().Build();

            var errorKey = "someValidationError";
            _controller.ModelState.AddModelError(errorKey, Guid.NewGuid().ToString());

            //Act
            var result = _controller.ConfirmEmail(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorKey));
            _userManagerMock.Verify(manager => manager.FindByIdAsync(It.IsAny<string>()), Times.Never);
            _userManagerMock.Verify(manager => manager.ConfirmEmailAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ConfirmEmailShouldReturnOkIfUserCannotBeFound()
        {
            //Arrange
            var model = new ConfirmEmailModelBuilder().Build();
            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(() => null);

            //Act
            var result = _controller.ConfirmEmail(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ConfirmEmailAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ConfirmEmailShouldReturnBadRequestIfConfirmationFails()
        {
            //Arrange
            var model = new ConfirmEmailModelBuilder().Build();
            var existingUser = new User();
            var errorCode = Guid.NewGuid().ToString();
            var identityResult = IdentityResult.Failed(new IdentityError
            {
                Code = errorCode,
                Description = Guid.NewGuid().ToString()
            });

            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(() => existingUser);
            _userManagerMock.Setup(manager => manager.ConfirmEmailAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(identityResult);

            //Act
            var result = _controller.ConfirmEmail(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorCode));

            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ConfirmEmailAsync(existingUser, model.Token), Times.Once);
        }

        [Test]
        public void ConfirmEmailShouldReturnOkIfConfirmationSucceeds()
        {
            //Arrange
            var model = new ConfirmEmailModelBuilder().Build();
            var existingUser = new User();

            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(() => existingUser);
            _userManagerMock.Setup(manager => manager.ConfirmEmailAsync(It.IsAny<User>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            //Act
            var result = _controller.ConfirmEmail(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ConfirmEmailAsync(existingUser, model.Token), Times.Once);
        }

        [Test]
        public void SendForgotPasswordMailShouldReturnBadRequestIfModelIsInvalid()
        {
            //Arrange
            var model = new ForgotPasswordModelBuilder().Build();

            var errorKey = "someValidationError";
            _controller.ModelState.AddModelError(errorKey, Guid.NewGuid().ToString());

            //Act
            var result = _controller.SendForgotPasswordMail(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorKey));
            _userManagerMock.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendForgotPasswordMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void SendForgotPasswordMailShouldReturnOkResultIfUserCannotBeFound()
        {
            //Arrange
            var model = new ForgotPasswordModelBuilder().Build();

            _userManagerMock.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(() => null);

            //Act
            var result = _controller.SendForgotPasswordMail(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByEmailAsync(model.Email), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendForgotPasswordMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void SendForgotPasswordMailShouldReturnOkResultIfUserEmailIsNotYetConfirmed()
        {
            //Arrange
            var model = new ForgotPasswordModelBuilder().Build();

            var existingUser = new User
            {
                EmailConfirmed = false
            };
            _userManagerMock.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            //Act
            var result = _controller.SendForgotPasswordMail(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByEmailAsync(model.Email), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendForgotPasswordMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void SendForgotPasswordMailShouldReturnBadRequestIfCaptchaIsInvalid()
        {
            //Arrange
            var model = new ForgotPasswordModelBuilder().WithValidEmail().Build();
            var expectedCaptchaResult = new CaptchaVerificationResult { Success = false };
            _captchaValidatorMock.Setup(validator => validator.Validate(It.IsAny<string>(), It.IsAny<IPAddress>()))
                .ReturnsAsync(expectedCaptchaResult);

            //Act
            var result = _controller.SendForgotPasswordMail(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _captchaValidatorMock.Verify(validator => validator.Validate(model.CaptchaToken, _controller.HttpContext.Connection.RemoteIpAddress));
            _userManagerMock.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
            _mailSenderMock.Verify(sender => sender.SendConfirmUserEmailMessageAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void SendForgotPasswordMailShouldCreatePasswordTokenAndSendConfirmationMail()
        {
            //Arrange
            var model = new ForgotPasswordModelBuilder().WithValidEmail().Build();

            var existingUser = new User
            {
                Email = model.Email,
                EmailConfirmed = true
            };
            _userManagerMock.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            var resetPasswordToken = Guid.NewGuid().ToString();
            _userManagerMock.Setup(manager => manager.GeneratePasswordResetTokenAsync(It.IsAny<User>()))
                .ReturnsAsync(resetPasswordToken);

            //Act
            var result = _controller.SendForgotPasswordMail(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);

            _userManagerMock.Verify(manager => manager.FindByEmailAsync(model.Email), Times.Once);
            _mailSenderMock.Verify(sender => sender.SendForgotPasswordMessageAsync(existingUser, resetPasswordToken), Times.Once);
        }

        [Test]
        public void ResetPasswordShouldReturnBadRequestIfModelIsInvalid()
        {
            //Arrange
            var model = new ResetPasswordModelBuilder().Build();

            var errorKey = "someValidationError";
            _controller.ModelState.AddModelError(errorKey, Guid.NewGuid().ToString());

            //Act
            var result = _controller.ResetPassword(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorKey));
            _userManagerMock.Verify(manager => manager.ResetPasswordAsync(It.IsAny<User>(),It.IsAny<string>() , It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ResetPasswordShouldReturnOkIfPasswordCouldBeChanged()
        {
            //Arrange
            var model = new ResetPasswordModelBuilder().Build();

            var existingUser = new User();
            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            _userManagerMock
                .Setup(mangager =>
                    mangager.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            //Act
            var result = _controller.ResetPassword(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ResetPasswordAsync(existingUser, model.Token, model.Password), Times.Once);
        }

        [Test]
        public void ResetPasswordShouldReturnOkIfUserCouldNotBeFound()
        {
            //Arrange
            var model = new ResetPasswordModelBuilder().Build();

            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(() => null);

            //Act
            var result = _controller.ResetPassword(model).Result as OkResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void ResetPasswordShouldReturnBadRequestIfPasswordCouldNotBeChanged()
        {
            //Arrange
            var model = new ResetPasswordModelBuilder().Build();

            var existingUser = new User();
            _userManagerMock.Setup(manager => manager.FindByIdAsync(It.IsAny<string>())).ReturnsAsync(existingUser);

            var errorCode = Guid.NewGuid().ToString();
            var identityResult = IdentityResult.Failed(new IdentityError
            {
                Code = errorCode,
                Description = Guid.NewGuid().ToString()
            });
            _userManagerMock
                .Setup(mangager =>
                    mangager.ResetPasswordAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(identityResult);

            //Act
            var result = _controller.ResetPassword(model).Result as BadRequestObjectResult;

            //Assert
            Assert.That(result, Is.Not.Null);
            var serializableError = result.Value as SerializableError;
            Assert.That(serializableError, Is.Not.Null);
            Assert.That(serializableError.Keys, Has.One.EqualTo(errorCode));

            _userManagerMock.Verify(manager => manager.FindByIdAsync(model.UserId), Times.Once);
            _userManagerMock.Verify(manager => manager.ResetPasswordAsync(existingUser, model.Token, model.Password), Times.Once);
        }

    }
}
