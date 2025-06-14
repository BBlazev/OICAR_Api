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
    public class OglasVoziloControllerTests : IDisposable
    {
        private readonly TestCarshareContext _ctx;
        private readonly OglasVoziloController _ctrl;
        private readonly AesEncryptionService _enc;

        public OglasVoziloControllerTests()
        {
            var opts = new DbContextOptionsBuilder<CarshareContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _ctx = new TestCarshareContext(opts);

            var aesKey = "MTIzNDU2Nzg5MDEyMzQ1Njc4OTAxMjM0NTY3ODkwMTI=";
            var settings = new Dictionary<string, string?>
            {
                { "AES:Key", aesKey }
            };
            IConfiguration cfg = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            _enc = new AesEncryptionService(cfg);
            _ctrl = new OglasVoziloController(_ctx, _enc);
        }

        public void Dispose() => _ctx.Dispose();

        private Korisnik SeedDriver(int id, string username, string ime, string prezime, string email)
        {
            var drv = new Korisnik
            {
                Idkorisnik = id,
                Username = _enc.Encrypt(username),
                Ime = _enc.Encrypt(ime),
                Prezime = _enc.Encrypt(prezime),
                Email = _enc.Encrypt(email),
                Pwdhash = "h",
                Pwdsalt = "s"
            };
            _ctx.Korisniks.Add(drv);
            return drv;
        }

        private Vozilo SeedVehicle(int vid, int driverId)
        {
            var v = new Vozilo { Idvozilo = vid, Marka = "M", Model = "X", Registracija = "R", Vozacid = driverId };
            _ctx.Vozilos.Add(v);
            return v;
        }

        private Oglasvozilo SeedAd(int adId, int vid, DateTime start, DateTime end)
        {
            var o = new Oglasvozilo
            {
                Idoglasvozilo = adId,
                Voziloid = vid,
                DatumPocetkaRezervacije = start,
                DatumZavrsetkaRezervacije = end
            };
            _ctx.Oglasvozilos.Add(o);
            return o;
        }

        private Korisnikvozilo SeedReservation(int resId, int adId, int userId, DateTime start, DateTime end)
        {
            var r = new Korisnikvozilo
            {
                Idkorisnikvozilo = resId,
                Oglasvoziloid = adId,
                Korisnikid = userId,
                DatumPocetkaRezervacije = start,
                DatumZavrsetkaRezervacije = end
            };
            _ctx.Korisnikvozilos.Add(r);
            return r;
        }

        [Fact]
        public async Task GetReservedDates_ReturnsCorrectRange()
        {
            var drv = SeedDriver(1, "U", "I", "P", "E");
            var veh = SeedVehicle(10, 1);
            var ad = SeedAd(100, 10, new DateTime(2025, 6, 10), new DateTime(2025, 6, 12));
            var res = SeedReservation(200, 100, 1, new DateTime(2025, 6, 10), new DateTime(2025, 6, 12));
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetReservedDates(100, 1);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var list = Assert.IsType<List<OglasVoziloDTO>>(ok.Value);
            var dto = Assert.Single(list);

            Assert.Equal(100, dto.IdOglasVozilo);
            Assert.Equal("M", dto.Marka);
            Assert.Equal(new[] { "2025-06-10", "2025-06-11", "2025-06-12" }, dto.ReservedDates);
        }

        [Fact]
        public async Task CreateReservation_NoOverlap_ReturnsOk()
        {
            var drv = SeedDriver(1, "U", "I", "P", "E");
            var veh = SeedVehicle(5, 1);
            await _ctx.SaveChangesAsync();

            var model = new VehicleReservationDTO
            {
                OglasVoziloId = 5,
                KorisnikId = 2,
                DatumPocetkaRezervacije = new DateTime(2025, 6, 1),
                DatumZavrsetkaRezervacije = new DateTime(2025, 6, 3)
            };

            var result = await _ctrl.CreateReservation(model);
            var ok = Assert.IsType<OkObjectResult>(result);
            var created = Assert.IsType<Korisnikvozilo>(ok.Value);
            Assert.Equal(2, created.Korisnikid);
        }

        [Fact]
        public async Task CreateReservation_Overlap_ReturnsBadRequest()
        {
            var drv = SeedDriver(1, "U", "I", "P", "E");
            var veh = SeedVehicle(6, 1);
            var existing = SeedReservation(1, 6, 2, new DateTime(2025, 6, 5), new DateTime(2025, 6, 7));
            await _ctx.SaveChangesAsync();

            var model = new VehicleReservationDTO
            {
                OglasVoziloId = 6,
                KorisnikId = 3,
                DatumPocetkaRezervacije = new DateTime(2025, 6, 6),
                DatumZavrsetkaRezervacije = new DateTime(2025, 6, 8)
            };

            var result = await _ctrl.CreateReservation(model);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetAll_ReturnsDecryptedDtos()
        {
            var drv = SeedDriver(1, "usr", "fn", "ln", "em");
            var veh = SeedVehicle(7, 1);
            var ad = SeedAd(50, 7, DateTime.Now, DateTime.Now.AddDays(1));
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetAll();
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var list = Assert.IsType<List<OglasVoziloDTO>>(ok.Value);
            var dto = Assert.Single(list);

            Assert.Equal(50, dto.IdOglasVozilo);
            Assert.Equal("M", dto.Marka);
            Assert.Equal("usr", dto.Username);
            Assert.Equal("fn", dto.Ime);
            Assert.Equal("ln", dto.Prezime);
            Assert.Equal("em", dto.Email);
        }

        [Fact]
        public async Task GetOglasVoziloById_NotFound()
        {
            var ar = await _ctrl.GetOglasVoziloById(999);
            Assert.IsType<NotFoundResult>(ar.Result);
        }

        [Fact]
        public async Task GetOglasVoziloById_ReturnsDto()
        {
            var drv = SeedDriver(2, "a", "b", "c", "d");
            var veh = SeedVehicle(8, 2);
            var ad = SeedAd(60, 8, DateTime.Today, DateTime.Today.AddDays(1));
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetOglasVoziloById(60);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var dto = Assert.IsType<OglasVoziloDTO>(ok.Value);

            Assert.Equal(60, dto.IdOglasVozilo);
            Assert.Equal("a", dto.Username);
        }

        [Fact]
        public async Task GetAllByUser_FiltersByOwner()
        {
            var drv1 = SeedDriver(1, "x", "i", "p", "e");
            var drv2 = SeedDriver(2, "y", "i", "p", "e");
            var v1 = SeedVehicle(1, 1);
            var v2 = SeedVehicle(2, 2);
            var a1 = SeedAd(101, 1, DateTime.Now, DateTime.Now);
            var a2 = SeedAd(102, 2, DateTime.Now, DateTime.Now);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetAllByUser(1);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var list = Assert.IsType<List<OglasVoziloDTO>>(ok.Value);
            Assert.Single(list);
            Assert.Equal(101, list[0].IdOglasVozilo);
        }

        [Fact]
        public async Task GetRentedCars_ExcludesOwner()
        {
            var drv1 = SeedDriver(1, "x", "i", "p", "e");
            var drv2 = SeedDriver(2, "y", "i", "p", "e");
            var v = SeedVehicle(3, 1);
            var ad = SeedAd(200, 3, DateTime.Now, DateTime.Now.AddDays(1));
            var r1 = SeedReservation(1, 200, 2, DateTime.Now, DateTime.Now.AddDays(1));
            var r2 = SeedReservation(2, 200, 1, DateTime.Now, DateTime.Now.AddDays(1));
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.GetRentedCars(2);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var list = Assert.IsType<List<OglasVoziloDTO>>(ok.Value);
            Assert.Single(list);
            Assert.Equal(200, list[0].IdOglasVozilo);
        }

        [Fact]
        public async Task DetaljiOglasaVozila_NotFound()
        {
            var ar = await _ctrl.DetaljiOglasaVozila(999);
            Assert.IsType<NotFoundResult>(ar.Result);
        }

        [Fact]
        public async Task DetaljiOglasaVozila_ReturnsDto()
        {
            var drv = SeedDriver(3, "u", "i", "p", "e");
            var veh = SeedVehicle(4, 3);
            var ad = SeedAd(300, 4, DateTime.Today, DateTime.Today.AddDays(2));
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.DetaljiOglasaVozila(300);
            var ok = Assert.IsType<OkObjectResult>(ar.Result);
            var dto = Assert.IsType<OglasVoziloDTO>(ok.Value);
            Assert.Equal(300, dto.IdOglasVozilo);
        }

        [Fact]
        public async Task KreirajOglasVozilo_NoExisting_Creates()
        {
            var drv = SeedDriver(5, "u", "i", "p", "e");
            await _ctx.SaveChangesAsync();

            var dto = new OglasVoziloDTO
            {
                VoziloId = 10,
                DatumPocetkaRezervacije = DateTime.Today,
                DatumZavrsetkaRezervacije = DateTime.Today.AddDays(1)
            };

            var ar = await _ctrl.KreirajOglasVozilo(dto);
            var ok = Assert.IsType<OkObjectResult>(ar);
            var outDto = Assert.IsType<OglasVoziloDTO>(ok.Value);
            Assert.True(outDto.IdOglasVozilo > 0);
        }

        [Fact]
        public async Task KreirajOglasVozilo_Existing_ReturnsBadRequest()
        {
            var drv = SeedDriver(6, "u", "i", "p", "e");
            var veh = SeedVehicle(20, 6);
            var ad = SeedAd(400, 20, DateTime.Now, DateTime.Now);
            await _ctx.SaveChangesAsync();

            var dto = new OglasVoziloDTO { VoziloId = 20 };
            var ar = await _ctrl.KreirajOglasVozilo(dto);
            Assert.IsType<BadRequestObjectResult>(ar);
        }

        [Fact]
        public async Task AzurirajOglasVozilo_NotFound()
        {
            var dto = new OglasVoziloDTO { VoziloId = 1 };
            var ar = await _ctrl.AzurirajOglasVozilo(999, dto);
            Assert.IsType<NotFoundResult>(ar);
        }

        [Fact]
        public async Task AzurirajOglasVozilo_Updates()
        {
            var drv = SeedDriver(7, "u", "i", "p", "e");
            var veh = SeedVehicle(30, 7);
            var ad = SeedAd(500, 30, DateTime.Today, DateTime.Today.AddDays(1));
            await _ctx.SaveChangesAsync();

            var dto = new OglasVoziloDTO
            {
                VoziloId = 30,
                DatumPocetkaRezervacije = DateTime.Today.AddDays(2),
                DatumZavrsetkaRezervacije = DateTime.Today.AddDays(3)
            };
            var ar = await _ctrl.AzurirajOglasVozilo(500, dto);
            var ok = Assert.IsType<OkObjectResult>(ar);
            var outDto = Assert.IsType<OglasVoziloDTO>(ok.Value);
            Assert.Equal(500, outDto.IdOglasVozilo);
            Assert.Equal(DateTime.Today.AddDays(2).Date, outDto.DatumPocetkaRezervacije.Date);
        }

        [Fact]
        public async Task ObrisiOglasVozilo_NotFound()
        {
            var ar = await _ctrl.ObrisiOglasVozilo(999);
            Assert.IsType<NotFoundResult>(ar);
        }

        [Fact]
        public async Task ObrisiOglasVozilo_Found()
        {
            var drv = SeedDriver(8, "u", "i", "p", "e");
            var veh = SeedVehicle(40, 8);
            var ad = SeedAd(600, 40, DateTime.Now, DateTime.Now);
            await _ctx.SaveChangesAsync();

            var ar = await _ctrl.ObrisiOglasVozilo(600);
            var ok = Assert.IsType<OkObjectResult>(ar);
            var deleted = Assert.IsType<Oglasvozilo>(ok.Value);
            Assert.Equal(600, deleted.Idoglasvozilo);
        }
    }
}
