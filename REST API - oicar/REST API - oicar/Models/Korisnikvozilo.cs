﻿using System;
using System.Collections.Generic;

namespace REST_API___oicar.Models;

public partial class Korisnikvozilo
{
    public int Idkorisnikvozilo { get; set; }

    public int? Korisnikid { get; set; }

    public DateTime DatumPocetkaRezervacije { get; set; }

    public DateTime DatumZavrsetkaRezervacije { get; set; }

    public int? Oglasvoziloid { get; set; }

    public virtual Korisnik? Korisnik { get; set; }

    public virtual Oglasvozilo? Oglasvozilo { get; set; }

    public virtual ICollection<Porukavozilo> Porukavozilos { get; set; } = new List<Porukavozilo>();
}
