using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using REST_API___oicar.Controllers;
using REST_API___oicar.DTOs;
using REST_API___oicar.Models;
using REST_API___oicar.Security;
using Xunit;

namespace APIUnitTests
{
    public class OglasVoznjaControllerTests : IDisposable
    {
        private readonly TestCarshareContext _ctx;
        private readonly OglasVoznjaController _ctrl;
        private readonly AesEncryptionService _encryptionService;

        public OglasVoznjaControllerTests()
        {
            var opts = new DbContextOptionsBuilder<CarshareContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _ctx = new TestCarshareContext(opts);

            var aesKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=";
            var inMemorySettings = new Dictionary<string, string?>
            {
                { "AES:Key", aesKey }
            };
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _encryptionService = new AesEncryptionService(config);
            _ctrl = new OglasVoznjaController(_ctx, _encryptionService);
        }

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public async Task GetAll_NoData_ReturnsEmptyList()
        {
            var actionResult = await _ctrl.GetAll();
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var list = Assert.IsType<List<OglasVoznjaDTO>>(ok.Value);
            Assert.Empty(list);
        }

        [Fact]
        public async Task GetAll_WithData_ReturnsMappedDto()
        {
            var vozac = new Korisnik
            {
                Idkorisnik = 1,
                Username = _encryptionService.Encrypt("User1"),
                Ime = _encryptionService.Encrypt("Ime1"),
                Prezime = _encryptionService.Encrypt("Prez1"),
                Email = "user1@example.com",
                Pwdhash = "dummyHash1",
                Pwdsalt = "dummySalt1"
            };
            var vozilo = new Vozilo { Idvozilo = 1, Marka = "Tesla", Model = "Model S", Registracija = "ZG1234", Vozacid = 1, Vozac = vozac };
            var troskovi = new Troskovi { Idtroskovi = 1, Cestarina = 50, Gorivo = 30 };
            var lokacija = new Lokacija { Idlokacija = 1, Polaziste = "A", Odrediste = "B" };
            var status = new Statusvoznje { Idstatusvoznje = 1, Naziv = "Scheduled" };
            var oglas = new Oglasvoznja
            {
                Idoglasvoznja = 1,
                Voziloid = 1,
                Vozilo = vozilo,
                Troskoviid = 1,
                Troskovi = troskovi,
                Lokacijaid = 1,
                Lokacija = lokacija,
                Statusvoznjeid = 1,
                Statusvoznje = status,
                DatumIVrijemePolaska = DateTime.Now,
                DatumIVrijemeDolaska = DateTime.Now.AddHours(2),
                BrojPutnika = 4
            };
            var booking = new Korisnikvoznja { Idkorisnikvoznja = 10, Oglasvoznjaid = 1, Korisnikid = 2 };

            _ctx.Korisnikvoznjas.Add(booking);
            _ctx.Korisniks.Add(vozac);
            _ctx.Vozilos.Add(vozilo);
            _ctx.Troskovis.Add(troskovi);
            _ctx.Lokacijas.Add(lokacija);
            _ctx.Statusvoznjes.Add(status);
            _ctx.Oglasvoznjas.Add(oglas);
            await _ctx.SaveChangesAsync();

            var actionResult = await _ctrl.GetAll();
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var list = Assert.IsType<List<OglasVoznjaDTO>>(ok.Value);
            var dto = Assert.Single(list);

            Assert.Equal(1, dto.IdOglasVoznja);
            Assert.Equal("Tesla", dto.Marka);
            Assert.Equal("User1", dto.Username);
            Assert.Equal((50 + 30) / 4, dto.CijenaPoPutniku);
            Assert.Equal(0, dto.PopunjenoMjesta);
        }

        [Fact]
        public async Task GetById_NotFound_ReturnsNotFound()
        {
            var actionResult = await _ctrl.GetById(999);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task GetById_Found_ReturnsDtoWithDecryptedValues()
        {
            var vozac = new Korisnik
            {
                Idkorisnik = 3,
                Username = _encryptionService.Encrypt("EncUser"),
                Ime = _encryptionService.Encrypt("EncIme"),
                Prezime = _encryptionService.Encrypt("EncPrez"),
                Email = "encuser@example.com",
                Pwdhash = "dummyHash3",
                Pwdsalt = "dummySalt3"
            };
            var vozilo = new Vozilo { Idvozilo = 2, Marka = "VW", Model = "Golf", Registracija = "ST5678", Vozacid = 3, Vozac = vozac };
            var troskovi = new Troskovi { Idtroskovi = 2, Cestarina = 20, Gorivo = 10 };
            var lokacija = new Lokacija { Idlokacija = 2, Polaziste = "X", Odrediste = "Y" };
            var status = new Statusvoznje { Idstatusvoznje = 2, Naziv = "Open" };
            var oglas = new Oglasvoznja
            {
                Idoglasvoznja = 2,
                Voziloid = 2,
                Vozilo = vozilo,
                Troskoviid = 2,
                Troskovi = troskovi,
                Lokacijaid = 2,
                Lokacija = lokacija,
                Statusvoznjeid = 2,
                Statusvoznje = status,
                DatumIVrijemePolaska = DateTime.Today,
                DatumIVrijemeDolaska = DateTime.Today.AddHours(1),
                BrojPutnika = 2
            };

            _ctx.Korisniks.Add(vozac);
            _ctx.Vozilos.Add(vozilo);
            _ctx.Troskovis.Add(troskovi);
            _ctx.Lokacijas.Add(lokacija);
            _ctx.Statusvoznjes.Add(status);
            _ctx.Oglasvoznjas.Add(oglas);
            await _ctx.SaveChangesAsync();

            var actionResult = await _ctrl.GetById(2);
            var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
            var dto = Assert.IsType<OglasVoznjaDTO>(ok.Value);

            Assert.Equal("EncUser", dto.Username);
            Assert.Equal("EncIme", dto.Ime);
            Assert.Equal("EncPrez", dto.Prezime);
        }

        [Fact]
        public async Task ObrisiOglasVoznje_NotFound_ReturnsNotFound()
        {
            var result = await _ctrl.ObrisiOglasVoznje(1234);
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ObrisiOglasVoznje_Found_RemovesAndReturnsOk()
        {
            var oglas = new Oglasvoznja { Idoglasvoznja = 5 };
            _ctx.Oglasvoznjas.Add(oglas);
            await _ctx.SaveChangesAsync();

            var result = await _ctrl.ObrisiOglasVoznje(5);
            var ok = Assert.IsType<OkObjectResult>(result);
            var deleted = Assert.IsType<Oglasvoznja>(ok.Value);

            Assert.Equal(5, deleted.Idoglasvoznja);
            Assert.Empty(_ctx.Oglasvoznjas);
        }
    }
}
