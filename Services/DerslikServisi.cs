#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami.Services
{
    /// <summary>Derslik Excel'i okunduktan sonra, uygulamadan önce gösterilen özet.</summary>
    public sealed class DerslikYuklemeOnizlemesi
    {
        public List<Salon> Salonlar { get; init; }
        public List<string> Uyarilar { get; init; }

        /// <summary>Listede yeni olan derslik kodları.</summary>
        public List<string> Eklenen { get; init; }

        /// <summary>Mevcut listede olup dosyada olmayan derslikler (silinecek).</summary>
        public List<string> Silinen { get; init; }

        /// <summary>Kapasitesi ya da türü değişen derslikler: "T1-107: 60 → 62 (uyg.)".</summary>
        public List<string> Degisen { get; init; }

        public int UygulamaliSayisi => Salonlar.Count(s => s.UygulamaliMi);
        public int NormalSayisi => Salonlar.Count - UygulamaliSayisi;
        public int ToplamKapasite => Salonlar.Sum(s => s.Kapasite);
        public bool Bos => Salonlar.Count == 0;
    }

    /// <summary>
    /// DERSLİKLER — okulun derslik Excel'i okunur, mevcut listeyle farkı gösterilir, onaylanırsa
    /// liste TAMAMEN bununla değiştirilir (eski BtnDerslikYukle_Click + DerslikListesiniUygula).
    /// Derslikler veritabanında değil oturum kaydında tutulur.
    /// </summary>
    public class DerslikServisi
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
        private readonly CalismaDurumu _d;

        public DerslikServisi(CalismaDurumu durum)
        {
            _d = durum;
        }

        /// <summary>
        /// Dosyayı okur ve verilen mevcut listeyle karşılaştırır. Çalışma durumuna dokunmaz —
        /// arka planda (IsYurutucu) güvenle çalışır; mevcut liste çağırandan kopya olarak verilir.
        /// </summary>
        public static DerslikYuklemeOnizlemesi Onizle(string dosyaYolu, IReadOnlyList<Salon> mevcut)
        {
            var okuma = DerslikListesiOkuyucu.Oku(dosyaYolu);

            var eskiMap = mevcut.GroupBy(s => s.SalonKodu.Trim(), StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var yeniKodlar = new HashSet<string>(okuma.Salonlar.Select(s => s.SalonKodu), StringComparer.OrdinalIgnoreCase);

            return new DerslikYuklemeOnizlemesi
            {
                Salonlar = okuma.Salonlar,
                Uyarilar = okuma.Uyarilar,
                Eklenen = okuma.Salonlar.Where(s => !eskiMap.ContainsKey(s.SalonKodu)).Select(s => s.SalonKodu).ToList(),
                Silinen = eskiMap.Keys.Where(k => !yeniKodlar.Contains(k)).OrderBy(k => k).ToList(),
                Degisen = okuma.Salonlar
                    .Where(s => eskiMap.TryGetValue(s.SalonKodu, out var o) &&
                                (o.Kapasite != s.Kapasite || o.UygulamaliMi != s.UygulamaliMi))
                    .Select(s =>
                    {
                        var o = eskiMap[s.SalonKodu];
                        return $"{s.SalonKodu}: {o.Kapasite}{(o.UygulamaliMi ? " (uyg.)" : "")} → {s.Kapasite}{(s.UygulamaliMi ? " (uyg.)" : "")}";
                    }).ToList()
            };
        }

        /// <summary>
        /// Onaylanan derslik listesini geçerli liste yapar (kapasiteye göre artan sıra). Salonlar
        /// değiştiği için önceki salon / gözetmen sonuçlarının dışa aktarımı kilitlenir.
        /// </summary>
        public void Uygula(List<Salon> yeniDerslikler)
        {
            _d.Derslikler = yeniDerslikler.OrderBy(s => s.Kapasite).ToList();

            var k = _d.Kilitler;
            k.SalonExport = false;
            k.GozetmenAta = false;
            k.MatrisExport = false;
            k.GozetmenliYukle = false;
            k.FinalExport = false;

            _d.DurumYaz($"✅ Derslik listesi güncellendi: {_d.Derslikler.Count} derslik. " +
                        "Salon ataması yaptıysanız 'Salonları Ata'yı yeniden çalıştırın.", DurumTuru.Basari);
            _d.Bildir();
        }

        /// <summary>Yüklü derslik listesinin özeti (eski alt durum satırı).</summary>
        public string DurumMetni()
        {
            if (_d.Derslikler.Count == 0)
                return "⚠️ Derslik listesi yüklenmedi — 'Derslik Listesi Yükle' ile okulunuzun dersliklerini yükleyin";
            int uyg = _d.Derslikler.Count(s => s.UygulamaliMi);
            return $"✅ {_d.Derslikler.Count} derslik yüklü ({_d.Derslikler.Count - uyg} normal, {uyg} uygulamalı) — " +
                   $"toplam kapasite {_d.Derslikler.Sum(s => s.Kapasite)}";
        }

        /// <summary>Derslik kodundan bina adı: "T4-204" → "T4" (tire yoksa boş).</summary>
        public static string Bina(string salonKodu)
        {
            if (string.IsNullOrWhiteSpace(salonKodu)) return "";
            int tire = salonKodu.IndexOf('-');
            return tire > 0 ? salonKodu.Substring(0, tire).Trim() : "";
        }

        public static string Sayi(int n) => n.ToString("N0", Tr);
    }
}
