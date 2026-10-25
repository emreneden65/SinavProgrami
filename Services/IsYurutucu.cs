#nullable disable
using System;
using System.Threading.Tasks;

namespace SinavProgrami.Services
{
    /// <summary>
    /// UZUN İŞLEM YÜRÜTÜCÜ — Excel okuma/yazma ve algoritmalar arka planda çalışır,
    /// pencere donmaz (eski arayüz bunları arayüz iş parçacığında Application.DoEvents ile yapıyordu).
    /// İşlem sürerken:
    ///   • arayüz bir katmanla kilitlenir (çift tıklama / araya giren düzenleme olmaz),
    ///   • otomatik oturum kaydı bekler (yarım değişmiş veri diske yazılmaz),
    ///   • sonuç ancak işlem bittikten sonra çalışma durumuna yayınlanır.
    /// </summary>
    public class IsYurutucu
    {
        /// <summary>Şu an bir uzun işlem sürüyor mu?</summary>
        public bool Mesgul { get; private set; }

        /// <summary>Kilit katmanında gösterilen açıklama ("Dersler dağıtılıyor…").</summary>
        public string Mesaj { get; private set; }

        public event Action Degisti;

        /// <summary>
        /// <paramref name="islem"/>'i arka planda çalıştırır ve sonucunu döner. Devamı çağıranın
        /// bağlamına (arayüz iş parçacığına) döner; sonucu çalışma durumuna orada işlemek güvenlidir.
        /// </summary>
        public async Task<T> CalistirAsync<T>(string mesaj, Func<T> islem)
        {
            if (Mesgul)
                throw new InvalidOperationException("Başka bir işlem sürüyor; bitmesini bekleyin.");

            Mesgul = true;
            Mesaj = mesaj;
            Degisti?.Invoke();
            try
            {
                // Kilit katmanı ekrana çizilsin, sonra işe başlansın
                await Task.Delay(40);
                return await Task.Run(islem);
            }
            finally
            {
                Mesgul = false;
                Mesaj = null;
                Degisti?.Invoke();
            }
        }

        public Task CalistirAsync(string mesaj, Action islem)
            => CalistirAsync(mesaj, () => { islem(); return true; });
    }
}
