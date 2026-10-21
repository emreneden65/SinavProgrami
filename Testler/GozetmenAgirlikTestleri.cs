using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;
using static SinavProgrami.Testler.TestKosucu;

namespace SinavProgrami.Testler;

/// <summary>
/// MADDE 4 — Gözetmen ağırlığı görev sayısına oranlı yansır:
/// ağırlık 1,0 olan 14 sınava giriyorsa 0,5 olan 7 sınava girer.
/// </summary>
static class GozetmenAgirlikTestleri
{
    private const string Bil = "Bilgisayar Mühendisliği";

    public static void Calistir(TestKosucu t)
    {
        t.Baslik("Madde 4 — Gözetmen ağırlığı");

        t.Test("Ağırlık 1,0 → 14 sınav ise 0,5 → 7 sınav (ortak dersler)", () =>
        {
            var g = Gozetmenler((1.0, "A"), (1.0, "B"), (0.5, "C"));
            Ata(SinavProgrami(slot: 35, salon: 1, ders: "MATEMATİK I"), g, ortak: true);
            SayilariDogrula(g, 14, 14, 7);
        });

        t.Test("Ağırlık 1,0 → 14 sınav ise 0,5 → 7 sınav (bölüm dersleri)", () =>
        {
            var g = Gozetmenler((1.0, "A"), (1.0, "B"), (0.5, "C"));
            Ata(SinavProgrami(slot: 35, salon: 1, ders: "VERİ YAPILARI"), g, ortak: false);
            SayilariDogrula(g, 14, 14, 7);
        });

        t.Test("Ağırlık 2,0 olan, 1,0 olanın iki katı görev alıyor", () =>
        {
            var g = Gozetmenler((2.0, "A"), (1.0, "B"));
            Ata(SinavProgrami(slot: 30, salon: 1, ders: "MATEMATİK I"), g, ortak: true);
            SayilariDogrula(g, 20, 10);
        });

        t.Test("Ağırlıklar eşitse dağılım eskisi gibi eşit (fark en fazla 1)", () =>
        {
            var g = Gozetmenler((1.0, "A"), (1.0, "B"), (1.0, "C"), (1.0, "D"));
            var program = SinavProgrami(slot: 35, salon: 2, ders: "MATEMATİK I");
            Ata(program, g, ortak: true);
            int enAz = g.Min(x => x.AtananSinavSayisi), enCok = g.Max(x => x.AtananSinavSayisi);
            Dogrula(enCok - enAz <= 1, $"Eşit ağırlıkta fark {enCok - enAz}: {Sayilar(g)}");
            CakismaYok(program);
        });

        t.Test("Karışık ağırlıklarda herkes hedefine ±1 yakın; aynı saatte iki salonda olan yok", () =>
        {
            var g = Gozetmenler((1.0, "A"), (1.0, "B"), (1.0, "C"), (1.0, "D"),
                                (1.0, "E"), (1.0, "F"), (0.5, "G"), (0.5, "H"));
            var program = SinavProgrami(slot: 35, salon: 3, ders: "VERİ YAPILARI");
            Ata(program, g, ortak: false);

            int toplam = g.Sum(x => x.AtananSinavSayisi);
            Dogrula(toplam == 105, $"Toplam görev {toplam}, beklenen 105 (boş salon kaldı).");
            double toplamAgirlik = g.Sum(x => x.Agirlik);
            foreach (var x in g)
            {
                double hedef = toplam * x.Agirlik / toplamAgirlik;
                Dogrula(Math.Abs(x.AtananSinavSayisi - hedef) <= 1.0,
                    $"{x.AdSoyad} (ağırlık {x.Agirlik}): {x.AtananSinavSayisi} sınav, hedef {hedef:0.0}. Tümü: {Sayilar(g)}");
            }
            CakismaYok(program);
        });

        t.Test("Ağırlıklı hedefler bölümlerin yüküne uyuyorsa bölüm dışına görev verilmiyor", () =>
        {
            // BİL: 12 sınav, MAK: 6 sınav. Ağırlıklar 1,0 / 0,5 / 0,75 → hedefler 8 / 4 / 6 (bölüm yüküyle aynı)
            var g = Gozetmenler((1.0, "A"), (0.5, "B"));
            g.Add(new Gozetmen { AdSoyad = "M", Bolum = "Makine Mühendisliği", Agirlik = 0.75 });
            var bil = SinavProgrami(slot: 12, salon: 1, ders: "VERİ YAPILARI");
            var mak = SinavProgrami(slot: 6, salon: 1, ders: "MUKAVEMET");
            foreach (var s in mak) { s.BolumAdi = "MAK"; s.Gun = s.Gun.AddDays(10); }
            Ata(bil.Concat(mak).ToList(), g, ortak: false);

            Dogrula(bil.SelectMany(s => s.AtananSalonlar).All(a => a.GozetmenAdSoyad is "A" or "B"),
                "Bilgisayar dersine bölüm dışından gözetmen atandı.");
            Dogrula(mak.SelectMany(s => s.AtananSalonlar).All(a => a.GozetmenAdSoyad == "M"),
                "Makine dersine bölüm dışından gözetmen atandı.");
            SayilariDogrula(g, 8, 4, 6);
        });

        OzetTesti(t);
    }

    /// <summary>Uygulamadaki atama özeti — ayrı çağrılır çünkü pencere açar.</summary>
    public static void OzetTesti(TestKosucu t)
    {
        t.Test("Atama sonrası mesajda ağırlığa göre dağılım özeti var", () =>
        {
            OturumKaydi.Sil();
            using var form = FormuAc();
            var liste = Alan<List<Gozetmen>>(form, "_gozetmenListesi");
            liste.AddRange(Gozetmenler((1.0, "A"), (1.0, "B"), (0.5, "C")));
            liste[0].AtananSinavSayisi = 14; liste[1].AtananSinavSayisi = 14; liste[2].AtananSinavSayisi = 7;
            string ozet = (string)Cagir(form, "AgirlikOzeti");
            Dogrula(ozet.Contains($"ağırlık {1.0:0.0#} → ortalama 14 sınav (2 kişi)") &&
                    ozet.Contains($"ağırlık {0.5:0.0#} → ortalama 7 sınav (1 kişi)"), $"Özet beklenen gibi değil:\n{ozet}");
            form.Close();
        });
    }

    private static List<Gozetmen> Gozetmenler(params (double Agirlik, string Ad)[] liste)
        => liste.Select(x => new Gozetmen { AdSoyad = x.Ad, Bolum = Bil, Agirlik = x.Agirlik }).ToList();

    /// <summary>Her slotta (gün + saat) bir BİL sınavı, belirtilen sayıda salonda.</summary>
    private static List<SinavAtama> SinavProgrami(int slot, int salon, string ders)
    {
        string[] saatler = { "09:00", "11:00", "13:00", "15:00", "17:00" };
        var liste = new List<SinavAtama>();
        for (int i = 0; i < slot; i++)
            liste.Add(new SinavAtama
            {
                DersAdi = ders, BolumAdi = "BİL", SinifSeviyesi = 1, Sube = "A", Kontenjan = 40 * salon,
                Gun = new DateTime(2026, 10, 26).AddDays(i / saatler.Length), Saat = saatler[i % saatler.Length],
                AtananSalonlar = Enumerable.Range(1, salon)
                    .Select(k => new SalonAtama { SalonKodu = $"T4-{100 + k}", SalonKapasitesi = 40, AtananOgrenciSayisi = 40 })
                    .ToList()
            });
        return liste;
    }

    private static void Ata(List<SinavAtama> program, List<Gozetmen> gozetmenler, bool ortak)
    {
        var algoritma = new GozetmenAtamaAlgoritmasi();
        var ortakDersler = ortak ? program.Select(s => s.DersAdi).Distinct().ToList() : new List<string>();
        algoritma.GozetmenleriAta(program, gozetmenler, ortakDersler);
    }

    private static void SayilariDogrula(List<Gozetmen> g, params int[] beklenen)
        => Dogrula(g.Select(x => x.AtananSinavSayisi).SequenceEqual(beklenen),
            $"Beklenen {string.Join(" / ", beklenen)}, çıkan {Sayilar(g)}");

    private static string Sayilar(List<Gozetmen> g)
        => string.Join(", ", g.Select(x => $"{x.AdSoyad}({x.Agirlik:0.0})={x.AtananSinavSayisi}"));

    /// <summary>K2 — bir gözetmen aynı slotta iki salonda olamaz.</summary>
    private static void CakismaYok(List<SinavAtama> program)
    {
        foreach (var slot in program.GroupBy(s => (s.Gun, s.Saat)))
        {
            var adlar = slot.SelectMany(s => s.AtananSalonlar).Select(a => a.GozetmenAdSoyad)
                            .Where(a => !string.IsNullOrEmpty(a)).ToList();
            Dogrula(adlar.Count == adlar.Distinct().Count(),
                $"{slot.Key.Gun:dd.MM} {slot.Key.Saat}: aynı gözetmen iki salonda ({string.Join(", ", adlar)}).");
        }
    }
}
