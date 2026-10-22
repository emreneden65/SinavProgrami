using SinavProgrami.Algorithm;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 11 — Gözetmen gün düzeni: aynı ağırlıktaki gözetmenlerin bir gündeki görev
/// sayıları dengeli (biri Pazar 3, diğeri 0 olmaz); bir gözetmenin aynı gündeki
/// görevleri yakın saatlerde (09:00 ile 17:00 aynı kişiye düşmez).
/// </summary>
static class GozetmenGunDuzeniTestleri
{
    private const string Bil = "Bilgisayar Mühendisliği";
    private static readonly DateTime Pazar = new(2026, 10, 25), Pazartesi = new(2026, 10, 26), Sali = new(2026, 10, 27);

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 11 — Gözetmen gün dengesi ve yakın saatler");

        t.Test("Pazar günü görevleri aynı ağırlıktaki 3 gözetmene birer birer dağılıyor", () =>
        {
            var g = Gozetmenler("A", "B", "C");
            var program = Program(
                (Pazar, "09:00"), (Pazar, "11:00"), (Pazar, "13:00"),
                (Pazartesi, "09:00"), (Pazartesi, "11:00"), (Pazartesi, "13:00"), (Pazartesi, "15:00"), (Pazartesi, "17:00"),
                (Sali, "09:00"), (Sali, "11:00"), (Sali, "13:00"), (Sali, "15:00"), (Sali, "17:00"));
            Ata(program, g);
            var pazar = GunSayilari(program, Pazar);
            Dogrula(g.All(x => pazar.GetValueOrDefault(x.AdSoyad) == 1),
                $"Pazar dağılımı: {string.Join(", ", g.Select(x => $"{x.AdSoyad}={pazar.GetValueOrDefault(x.AdSoyad)}"))}");
            ToplamDengesi(g);
        });

        t.Test("İki günde 8 görev: her gözetmen her gün 2 göreve, art arda saatlerde giriyor", () =>
        {
            var g = Gozetmenler("A", "B");
            var program = Program(
                (Pazar, "09:00"), (Pazar, "11:00"), (Pazar, "13:00"), (Pazar, "15:00"),
                (Pazartesi, "09:00"), (Pazartesi, "11:00"), (Pazartesi, "13:00"), (Pazartesi, "15:00"));
            Ata(program, g);
            foreach (var gun in new[] { Pazar, Pazartesi })
            {
                var sayilar = GunSayilari(program, gun);
                Dogrula(g.All(x => sayilar.GetValueOrDefault(x.AdSoyad) == 2),
                    $"{gun:dd.MM}: {string.Join(", ", sayilar.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            ArtArdaSaatler(program);
        });

        t.Test("Aynı gün 09:00 ve 17:00 aynı gözetmene düşmüyor, saatler bitişik", () =>
        {
            var g = Gozetmenler("A", "B");
            var program = Program((Pazartesi, "09:00"), (Pazartesi, "11:00"), (Pazartesi, "13:00"),
                                  (Pazartesi, "15:00"), (Pazartesi, "17:00"));
            Ata(program, g);
            foreach (var x in g)
            {
                var saatler = Saatler(program, x.AdSoyad, Pazartesi);
                Dogrula(!(saatler.Contains(9) && saatler.Contains(17)), $"{x.AdSoyad}: {string.Join(", ", saatler)}");
            }
            ArtArdaSaatler(program);
            ToplamDengesi(g);
        });

        t.Test("Takaslar toplam görev sayısını ve çakışma kuralını bozmuyor (2 salonlu büyük program)", () =>
        {
            var g = Gozetmenler("A", "B", "C", "D", "E");
            string[] saat = { "09:00", "11:00", "13:00", "15:00", "17:00" };
            var slotlar = Enumerable.Range(0, 35).Select(i => (Pazar.AddDays(i / 5), saat[i % 5])).ToArray();
            var program = Program(slotlar, salon: 2);
            Ata(program, g);
            ToplamDengesi(g);
            foreach (var slot in program.GroupBy(s => (s.Gun, s.Saat)))
            {
                var adlar = slot.SelectMany(s => s.AtananSalonlar).Select(a => a.GozetmenAdSoyad).ToList();
                Dogrula(adlar.All(a => a != "") && adlar.Count == adlar.Distinct().Count(),
                    $"{slot.Key.Gun:dd.MM} {slot.Key.Saat}: {string.Join(", ", adlar)}");
            }
        });
    }

    private static List<Gozetmen> Gozetmenler(params string[] adlar)
        => adlar.Select(a => new Gozetmen { AdSoyad = a, Bolum = Bil, Agirlik = 1.0 }).ToList();

    private static List<SinavAtama> Program(params (DateTime Gun, string Saat)[] slotlar) => Program(slotlar, 1);

    private static List<SinavAtama> Program((DateTime Gun, string Saat)[] slotlar, int salon)
        => slotlar.Select(s => new SinavAtama
        {
            DersAdi = "VERİ YAPILARI", BolumAdi = "BİL", SinifSeviyesi = 1, Sube = "A", Kontenjan = 40 * salon,
            Gun = s.Gun, Saat = s.Saat,
            AtananSalonlar = Enumerable.Range(1, salon)
                .Select(k => new SalonAtama { SalonKodu = $"T4-{100 + k}", SalonKapasitesi = 40, AtananOgrenciSayisi = 40 })
                .ToList()
        }).ToList();

    private static void Ata(List<SinavAtama> program, List<Gozetmen> g)
        => new GozetmenAtamaAlgoritmasi().GozetmenleriAta(program, g, new List<string>());

    private static Dictionary<string, int> GunSayilari(List<SinavAtama> program, DateTime gun)
        => program.Where(s => s.Gun == gun).SelectMany(s => s.AtananSalonlar)
                  .GroupBy(a => a.GozetmenAdSoyad).ToDictionary(gr => gr.Key, gr => gr.Count());

    private static List<int> Saatler(List<SinavAtama> program, string ad, DateTime gun)
        => program.Where(s => s.Gun == gun && s.AtananSalonlar.Any(a => a.GozetmenAdSoyad == ad))
                  .Select(s => int.Parse(s.Saat[..2])).OrderBy(h => h).ToList();

    /// <summary>Her gözetmenin her gündeki görevleri 2 saat arayla art arda.</summary>
    private static void ArtArdaSaatler(List<SinavAtama> program)
    {
        var adlar = program.SelectMany(s => s.AtananSalonlar).Select(a => a.GozetmenAdSoyad).Distinct();
        foreach (var ad in adlar)
            foreach (var gun in program.Select(s => s.Gun).Distinct())
            {
                var h = Saatler(program, ad, gun);
                for (int i = 1; i < h.Count; i++)
                    Dogrula(h[i] - h[i - 1] <= 2, $"{ad} {gun:dd.MM}: saatler bitişik değil ({string.Join(", ", h)})");
            }
    }

    /// <summary>Eşit ağırlıkta toplam görev farkı en fazla 1 (Faz 3 bunu bozmamalı).</summary>
    private static void ToplamDengesi(List<Gozetmen> g)
    {
        int enAz = g.Min(x => x.AtananSinavSayisi), enCok = g.Max(x => x.AtananSinavSayisi);
        Dogrula(enCok - enAz <= 1, $"Toplam görev farkı {enCok - enAz}: " +
            string.Join(", ", g.Select(x => $"{x.AdSoyad}={x.AtananSinavSayisi}")));
    }
}
