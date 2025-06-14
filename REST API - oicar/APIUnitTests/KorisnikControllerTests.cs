using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using REST_API___oicar.Controllers;
using REST_API___oicar.DTOs;
using REST_API___oicar.Models;
using REST_API___oicar.Security;
using Xunit;

namespace APIUnitTests
{
    public class KorisnikControllerTests : IDisposable
    {
        private readonly TestCarshareContext _ctx;
        private readonly KorisnikController _ctrl;
        private readonly AesEncryptionService _enc;
        private readonly IConfiguration _cfg;

        public KorisnikControllerTests()
        {
            var opts = new DbContextOptionsBuilder<CarshareContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _ctx = new TestCarshareContext(opts);

            var dict = new Dictionary<string, string?>
            {
                ["AES:Key"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                ["Jwt:SecureKey"] = "supersecretjwtkey123"
            };
            _cfg = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
            _enc = new AesEncryptionService(_cfg);
            _ctrl = new KorisnikController(_ctx, _cfg, _enc);
        }

        public void Dispose() => _ctx.Dispose();

        private Korisnik SeedUser(int id, string username, string ime, string prezime, string email, DateTime dob, int ulogaId)
        {
            var u = new Korisnik
            {
                Idkorisnik = id,
                Username = _enc.Encrypt(username),
                Ime = _enc.Encrypt(ime),
                Prezime = _enc.Encrypt(prezime),
                Email = _enc.Encrypt(email),
                Telefon = _enc.Encrypt("123"),
                Datumrodjenja = DateOnly.MinValue,
                Ulogaid = ulogaId,
                Pwdhash = "h",
                Pwdsalt = "s",
                Isconfirmed = true
            };
            _ctx.Korisniks.Add(u);
            return u;
        }

        [Fact]
        public async Task Update_NotFound()
        {
            var dto = new KorisnikUpdateDTO { Ime = "A", Prezime = "B", Email = "e", Telefon = "t" };
            var ar = await _ctrl.Update(999, dto);
            Assert.IsType<NotFoundObjectResult>(ar.Result);
        }

        [Fact]
        public async Task Update_Success()
        {
            SeedUser(1, "usr", "I", "P", "e@mail.com", DateTime.Today, 2);
            await _ctx.SaveChangesAsync();

            var dto = new KorisnikUpdateDTO { Ime = "NewI", Prezime = "NewP", Email = "new@mail.com", Telefon = "999" };
            var ar = await _ctrl.Update(1, dto);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task ClearUserInfo_NotFound()
        {
            var ar = await _ctrl.ClearUserInfo(999);
            Assert.IsType<NotFoundObjectResult>(ar);
        }

        [Fact]
        public async Task ClearUserInfo_Success()
        {
            SeedUser(2, "u", "i", "p", "e@mail", DateTime.Today, 1);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.ClearUserInfo(2);
            var ok = Assert.IsType<OkObjectResult>(ar);
            Assert.Contains("cleared", ok.Value as string);

            var fresh = await _ctx.Korisniks.FindAsync(2);
            Assert.StartsWith("Anonymous_", fresh.Ime);
        }

        [Fact]
        public async Task RequestClearInfo_SetsTelefon()
        {
            SeedUser(3, "u", "i", "p", "e@mail", DateTime.Today, 1);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.RequestClearInfo(3);
            var ok = Assert.IsType<OkObjectResult>(ar);
            Assert.Contains("requested", ok.Value as string);

            var fresh = await _ctx.Korisniks.FindAsync(3);
            Assert.Equal("Request to clear data", fresh.Telefon);
        }


        [Fact]
        public async Task GetAllRequestClear_FindsRequested()
        {
            var u = SeedUser(6, "a", "i", "p", "e", DateTime.Today, 4);
            SeedUser(7, "b", "i", "p", "e", DateTime.Today, 2);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetAllRequestClear();
            var list = Assert.IsType<List<KorisnikDTO>>(ar.Value);
            Assert.Single(list);
            Assert.Equal(6, list[0].IDKorisnik);
        }

        [Fact]
        public async Task GetById_NotFound()
        {
            var ar = await _ctrl.GetById(999);
            Assert.IsType<NotFoundResult>(ar.Result);
        }

        [Fact]
        public async Task GetById_Found()
        {
            SeedUser(8, "u", "i", "p", "e", DateTime.Today, 1);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetById(8);
            var ok = Assert.IsType<Korisnik>(ar.Value);
            Assert.Equal(8, ok.Idkorisnik);
        }

        [Fact]
        public async Task Details_NotFound()
        {
            var ar = await _ctrl.Details(999);
            Assert.IsType<NotFoundObjectResult>(ar.Result);
        }

        [Fact]
        public async Task Details_ReturnsDto()
        {
            SeedUser(9, "x", "I", "P", "e", DateTime.Today, 2);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.Details(9);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var dto = Assert.IsType<KorisnikDTO>(ok.Value);
            Assert.Equal("I", dto.Ime);
        }

        [Fact]
        public async Task Profile_NotFound()
        {
            var ar = await _ctrl.Profile(999);
            Assert.IsType<NotFoundObjectResult>(ar.Result);
        }

        [Fact]
        public async Task Profile_ReturnsDto()
        {
            SeedUser(10, "y", "F", "L", "e", DateTime.Today, 3);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.Profile(10);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var dto = Assert.IsType<KorisnikDTO>(ok.Value);
            Assert.Equal("F", dto.Ime);
        }

        [Fact]
        public async Task Registracija_Success()
        {
            var dto = new KorisnikRegistracijaDTO
            {
                Username = "newuser",
                Ime = "I",
                Prezime = "P",
                Email = "e",
                Telefon = "t",
                Datumrodjenja = DateOnly.MinValue,
                Password = "pwd"
            };
            var ar = await _ctrl.Registracija(dto);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var outDto = Assert.IsType<KorisnikRegistracijaDTO>(ok.Value);
            Assert.True(outDto.Id > 0);
        }

        [Fact]
        public async Task GetDecryptedUser_NotFound()
        {
            var ar = await _ctrl.GetDecryptedUser(999);
            Assert.IsType<NotFoundObjectResult>(ar.Result);
        }

        [Fact]
        public void ChangePassword_NoInput_BadRequest()
        {
            var dto = new KorisnikPromjenaLozinkeDTO();
            var result = _ctrl.ChangePassword(dto);
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
