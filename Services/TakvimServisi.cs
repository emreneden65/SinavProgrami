#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>"Takvimi Doldur" onayında gösterilen hafta satırı.</summary>
    public sealed class TakvimHaftasi
    {
        public int No { get; init; }
        public DateTime Ilk { get; init; }
        public DateTime Son { get; init; }
        public int GunSayisi { get; init; }

        public override string ToString() => $"{No}. Hafta: {Ilk:dd.MM} – {Son:dd.MM}  ({GunSayisi} gün)";
    }

    /// <summary>"Takvimi Doldur" öncesi kullanıcıya gösterilecek özet (eski BtnTakvimDoldur_Click onayı).</summary>
    public sealed class TakvimDoldurmaOnizlemesi
    {
        public List<DateTime> Gunler { get; init; }
        public string TipAdi { get; init; }
        public int GunSayisi { get; init; }

        /// <summary>"Pazartesi–Cuma — HAFTA SONLARI BOŞ" ya da "hafta sonları dahil her gün sınav günü".</summary>
        public string Kural { get; init; }
        public List<TakvimHaftasi> Haftalar { get; init; }

        /// <summary>Takvimde zaten seçili olup silinecek gün sayısı (0 = takvim boş).</summary>
        public int SilinecekGunSayisi { get; init; }
    }

    /// <summary>
    /// TAKVİM VE ÖĞRETİM DÖNEMİ — sınav türü, sınav günleri / saatleri, öğretim yılı ve dönem.
    /// Eski arayüzdeki Sınav Takvimi bölümünün (PrepopulateDefaultCalendar, BtnTakvimDoldur_Click,
    /// ChkGunEtkin_CheckedChanged, HourCheckBox_CheckedChanged, öğretim yılı/dönem kontrolleri)
    /// kurallarının arayüzden bağımsız hali.
    /// </summary>
    public class TakvimServisi
    {
        /// <summary>Bir gün sınav günü yapıldığında otomatik işaretlenen varsayılan saatler.</summary>
        public static readonly string[] VarsayilanSinavSaatleri = { "09:00", "11:00", "13:00", "15:00", "17:00" };

        /// <summary>Saat seçenekleri: 09:00'dan 17:00'a kadar 1 saat aralıklı.</summary>
        public static readonly string[] SaatSecenekleri = Enumerable.Range(9, 9).Select(h => $"{h:00}:00").ToArray();

        private readonly CalismaDurumu _d;

        public TakvimServisi(CalismaDurumu durum)
        {
            _d = durum;
        }

        // ═══════════════════════════════════════════════════════════════
        //  SINAV TÜRÜ VE TAKVİM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Kayıt yokken açılışta: seçili türün gün sayısı kadar gün, yarından itibaren.</summary>
        public void VarsayilanTakvimiKur()
        {
            _d.TakvimSlotlari.Clear();
            foreach (var g in SinavTipi.GunleriUret(DateTime.Today.AddDays(1), _d.SinavTipi))
                _d.TakvimSlotlari[g] = new List<string>(VarsayilanSinavSaatleri);
            SinavTipiDurumunuYaz();
            _d.Bildir();
        }

        /// <summary>Sınav türü değişince gün sayısı kutusuna o türün varsayılanı önerilir (değiştirilebilir).</summary>
        public void SinavTipiSec(SinavDonemTipi tip)
        {
            _d.SinavTipi = tip;
            _d.GunSayisi = SinavTipi.VarsayilanGunSayisi(tip);
            SinavTipiDurumunuYaz();
            _d.Bildir();
        }

        public void GunSayisiAyarla(int gunSayisi)
        {
            _d.GunSayisi = Math.Max(1, Math.Min(SinavTipi.EnFazlaGunSayisi, gunSayisi));
            _d.Bildir();
        }

        public void BaslangicTarihiAyarla(DateTime tarih)
        {
            _d.BaslangicTarihi = tarih.Date;
            _d.Bildir();
        }

        /// <summary>Seçili tip ve takvimdeki gün sayısı — gün sayısı serbest olduğu için hedef/uyarı yok.</summary>
        public void SinavTipiDurumunuYaz()
        {
            int mevcut = _d.TakvimSlotlari.Count;
            _d.DurumYaz(mevcut == 0
                    ? $"🗓 {SinavTipi.Ad(_d.SinavTipi)} — takvimde henüz sınav günü seçilmedi."
                    : $"🗓 {SinavTipi.Ad(_d.SinavTipi)} — takvimde {mevcut} sınav günü seçili.",
                mevcut == 0 ? DurumTuru.Uyari : DurumTuru.Basari);
        }

        /// <summary>"Takvimi Doldur" onayı için üretilecek günler ve hafta özeti.</summary>
        public TakvimDoldurmaOnizlemesi DoldurmaOnizlemesi()
        {
            var gunler = SinavTipi.GunleriUret(_d.BaslangicTarihi.Date, _d.SinavTipi, _d.GunSayisi);
            return new TakvimDoldurmaOnizlemesi
            {
                Gunler = gunler,
                TipAdi = SinavTipi.Ad(_d.SinavTipi),
                GunSayisi = _d.GunSayisi,
                Kural = !SinavTipi.CumartesiSinavGunuMu(_d.SinavTipi)
                    ? "Pazartesi–Cuma — HAFTA SONLARI BOŞ"
                    : "hafta sonları dahil her gün sınav günü",
                Haftalar = gunler
                    .GroupBy(g => SinavTipi.HaftaNo(g, gunler))
                    .OrderBy(g => g.Key)
                    .Select(g => new TakvimHaftasi { No = g.Key, Ilk = g.Min(), Son = g.Max(), GunSayisi = g.Count() })
                    .ToList(),
                SilinecekGunSayisi = _d.TakvimSlotlari.Count
            };
        }

        /// <summary>Takvimi verilen günlerle baştan kurar; her güne varsayılan saatler yazılır.</summary>
        public void TakvimiDoldur(IReadOnlyCollection<DateTime> gunler)
        {
            if (gunler == null || gunler.Count == 0) return;
            _d.TakvimSlotlari.Clear();
            foreach (var g in gunler)
                _d.TakvimSlotlari[g.Date] = new List<string>(VarsayilanSinavSaatleri);
            SinavTipiDurumunuYaz();
            _d.Bildir();
        }

        public bool SinavGunuMu(DateTime gun) => _d.TakvimSlotlari.ContainsKey(gun.Date);

        /// <summary>Final'de hafta sonları normalde boş bırakılır — elle işaretlemede uyarılır.</summary>
        public bool HaftaSonuUyarisiGerekir(DateTime gun)
        {
            bool haftaSonu = gun.DayOfWeek == DayOfWeek.Saturday || gun.DayOfWeek == DayOfWeek.Sunday;
            return haftaSonu && !SinavTipi.CumartesiSinavGunuMu(_d.SinavTipi);
        }

        public string HaftaSonuUyariMetni(DateTime gun)
            => $"{gun:dd.MM.yyyy} bir hafta sonu günü.\n\n" +
               $"{SinavTipi.Ad(_d.SinavTipi)}nda hafta sonları normalde boş bırakılır.\n\n" +
               "Yine de sınav günü yapılsın mı?";

        /// <summary>Günü sınav günü yapar (varsayılan saatlerle). Hafta sonu onayı çağıran tarafta alınır.</summary>
        public void SinavGunuYap(DateTime gun)
        {
            gun = gun.Date;
            if (!_d.TakvimSlotlari.ContainsKey(gun))
                _d.TakvimSlotlari[gun] = new List<string>(VarsayilanSinavSaatleri);
            SinavTipiDurumunuYaz();
            _d.Bildir();
        }

        /// <summary>Günü takvimden çıkarır; "geri al" için o günün saatlerini döner (gün yoksa null).</summary>
        public List<string> SinavGununuKaldir(DateTime gun)
        {
            gun = gun.Date;
            if (!_d.TakvimSlotlari.TryGetValue(gun, out var saatler)) return null;
            _d.TakvimSlotlari.Remove(gun);
            SinavTipiDurumunuYaz();
            _d.Bildir();
            return saatler;
        }

        /// <summary>Çıkarılan günü saatleriyle birlikte geri koyar ("geri al").</summary>
        public void GunuGeriKoy(DateTime gun, List<string> saatler)
        {
            if (saatler == null || saatler.Count == 0) return;
            _d.TakvimSlotlari[gun.Date] = saatler.ToList();
            SinavTipiDurumunuYaz();
            _d.Bildir();
        }

        /// <summary>
        /// Bir günün tek saatini açar / kapatır. Günün hiç saati kalmazsa gün takvimden
        /// çıkarılır ve true döner (eski HourCheckBox_CheckedChanged).
        /// </summary>
        public bool SaatAyarla(DateTime gun, string saat, bool secili)
        {
            gun = gun.Date;
            if (!_d.TakvimSlotlari.TryGetValue(gun, out var saatler)) return false;

            bool gunCikti = false;
            if (secili)
            {
                if (!saatler.Contains(saat))
                {
                    saatler.Add(saat);
                    saatler.Sort();
                }
            }
            else
            {
                saatler.Remove(saat);
                if (saatler.Count == 0)
                {
                    _d.TakvimSlotlari.Remove(gun);
                    gunCikti = true;
                    SinavTipiDurumunuYaz();
                }
            }
            _d.Bildir();
            return gunCikti;
        }

        /// <summary>Bir günün gösterilecek saatleri: 09–17 seçenekleri + o güne özel (ör. şablondan gelen) saatler.</summary>
        public List<string> GununSaatSecenekleri(DateTime gun)
        {
            var liste = SaatSecenekleri.ToList();
            if (_d.TakvimSlotlari.TryGetValue(gun.Date, out var saatler))
                liste.AddRange(saatler.Where(s => !liste.Contains(s)));
            return liste.OrderBy(SaatYardimci.Anahtar).ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        //  ÖĞRETİM YILI + DÖNEM (Güz / Bahar)
        //  İkisi de boş gelir ve Excel çıktısı almadan önce seçilmek zorundadır;
        //  final / gözetmenli Excel başlıkları bu seçimden üretilir.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Listedeki öneriler (geçen öğretim yılından iki yıl sonrasına kadar), "2026/2027" biçiminde.</summary>
        public static List<string> OgretimYiliSecenekleri()
            => AkademikDonem.YilSecenekleri(DateTime.Today).Select(AkademikDonem.YilGorunen).ToList();

        /// <summary>Kutuya yazılan metni öğretim yılına çevirir (listeden seçilebilir veya elle yazılabilir).</summary>
        public void OgretimYiliMetniniAyarla(string metin)
        {
            _d.OgretimYiliMetni = metin ?? "";
            _d.OgretimYili = AkademikDonem.YilCoz(_d.OgretimYiliMetni);
            _d.Bildir();
        }

        /// <summary>Yazılmış ama çözülemeyen yıl (kutu uyarı rengi alır).</summary>
        public bool OgretimYiliGecersiz
            => _d.OgretimYili == null && !string.IsNullOrWhiteSpace(_d.OgretimYiliMetni);

        /// <summary>Kutudan çıkınca geçerli yazım standart biçime getirilir: "2031-2032" / "2031" → "2031/2032".</summary>
        public void OgretimYiliMetniniDuzelt()
        {
            if (_d.OgretimYili.HasValue && _d.OgretimYiliMetni != AkademikDonem.YilGorunen(_d.OgretimYili.Value))
            {
                _d.OgretimYiliMetni = AkademikDonem.YilGorunen(_d.OgretimYili.Value);
                _d.Bildir();
            }
        }

        public void YariyilSec(Yariyil? yariyil)
        {
            _d.Yariyil = yariyil;
            _d.Bildir();
        }

        /// <summary>
        /// Excel çıktısından önce çağrılır: öğretim yılı veya dönem eksikse kullanıcıya gösterilecek
        /// mesajı, ikisi de seçiliyse null döner (eski OgretimDonemiSecildiMi).
        /// </summary>
        public string OgretimDonemiEksikMesaji()
        {
            var eksik = new List<string>();
            if (_d.OgretimYili == null)
                eksik.Add(string.IsNullOrWhiteSpace(_d.OgretimYiliMetni)
                    ? "öğretim yılı"
                    : $"geçerli bir öğretim yılı ('{_d.OgretimYiliMetni.Trim()}' geçersiz — 2026/2027 biçiminde yazın)");
            if (_d.Yariyil == null) eksik.Add("dönem (Güz / Bahar)");
            if (eksik.Count == 0) return null;

            return $"Önce {string.Join(" ve ", eksik)} seçilmeli.\n\n" +
                   "'1 · Dönem ve Takvim' sayfasının en üstündeki 'Öğretim dönemi' bölümünden seçin. " +
                   "Excel başlıkları (ör. \"2026-2027 ÖĞRETİM YILI GÜZ DÖNEMİ VİZE SINAV PROGRAMI\") bu seçimden oluşur.";
        }

        /// <summary>Seçili öğretim yılı, dönem ve sınav türünden Excel program başlığı.</summary>
        public string ProgramBasligi()
            => AkademikDonem.ProgramBasligi(_d.OgretimYili.Value, _d.Yariyil.Value, _d.SinavTipi);

        /// <summary>Başlık önizlemesi; öğretim yılı veya dönem seçilmemişse null.</summary>
        public string ProgramBasligiOnizlemesi
            => _d.OgretimYili.HasValue && _d.Yariyil.HasValue ? ProgramBasligi() : null;
    }
}
