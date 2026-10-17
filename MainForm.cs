#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using SinavProgrami.Algorithm;
using SinavProgrami.DataAccess;
using SinavProgrami.Models;

namespace SinavProgrami
{
    public partial class MainForm : Form
    {
        private readonly List<Ders> _dersListesi = new List<Ders>();
        private List<SinavAtama> _sonSonuclar = null;
        // Salon atama sonucu (gün-sayfası matrisi) — 'Salonları Ata' üretir, 'Salon Atanmış Dosya Çıkart' kullanır.
        private List<GunlukSalonDagitici.GunSatir> _gunSalonSatirlari = null;

        // Kullanıcı tanımlı şube havuzları — seçilen şubeler salon atamasında tek salonda birleştirilir
        private readonly List<DersHavuzu> _sablonHavuzlari = new List<DersHavuzu>();

        // MANUEL ORTAK DERS SEÇİMİ — kullanıcı seçim yaptıysa otomatik tespit devre dışı kalır
        private bool _ortakSecimYapildi = false;
        private readonly HashSet<string> _manuelOrtakKodlar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _manuelOrtakDersAdlari = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdating = false;

        // ── AŞAMALI AKIŞ: ŞABLON DURUMU ──
        // Aşama A'da oluşturulan (veya boş açılan) salonsuz takvim yerleşimi.
        // Grid düzenlemeleri bu listeyi günceller; SalonlariDagit bu listeyi okur.
        private List<SinavAtama> _sablonSonuclari = null;
        // Kullanıcı şablon üzerinde elle değişiklik yaptı mı? (yeniden oluşturma onayı için)
        private bool _sablonDuzenlendi = false;
        // Şablon düzenlemelerinde kural ihlallerini anında kontrol eden doğrulayıcı
        private SablonDogrulayici _sablonDogrulayici = null;
        // INT sütunundaki elle girilen notlar (grid yenilense de kaybolmasın)
        private readonly Dictionary<string, string> _sablonIntNotlari = new Dictionary<string, string>();
        // Şablon grid'inin gün / saat / bölüm eksenleri
        private List<DateTime> _sablonGunler = new List<DateTime>();
        private List<string> _sablonSaatler = new List<string>();
        private List<(string Kisa, string Tam)> _sablonBolumler = new List<(string, string)>();

        // Şablon zoom (Ctrl + fare tekerleği) durumu — matris zoom ile aynı mantık
        private const float SablonBazFontBoyutu = 9F;
        private const float SablonMinFontBoyutu = 6F;
        private const float SablonMaxFontBoyutu = 16F;
        private float _sablonFontBoyutu = SablonBazFontBoyutu;
        private readonly Dictionary<string, int> _sablonBazSutunGenislikleri = new Dictionary<string, int>();
        private int _sablonBazSatirYuksekligi;
        // Gün başlık bandının yüksekliği (birleşik gün başlığı owner-draw için, zoom ile ölçeklenir)
        private int SablonGunBandiYuksekligi => Math.Max(14, (int)Math.Round(22 * _sablonFontBoyutu / SablonBazFontBoyutu));
        // Başlık hücrelerinin/gün bandının zoom'la ölçeklenen fontu
        private float SablonBaslikFontBoyutu => Math.Max(6F, 8.5F * _sablonFontBoyutu / SablonBazFontBoyutu);

        // Takvim veri yapısı
        private readonly Dictionary<DateTime, List<string>> _takvimSlotlari = new Dictionary<DateTime, List<string>>();

        // ── SINAV DÖNEMİ TİPİ (Vize 1 hafta / Final 2 hafta) ──
        // Takvim doldurma, şablon Excel bant sayısı, algoritmanın yayılma kuralı ve
        // çıktılardaki hafta ayrımı bu seçime göre belirlenir.
        private SinavDonemTipi _sinavTipi = SinavDonemTipi.Vize;
        private ComboBox cmbSinavTipi;
        private DateTimePicker dtpSinavBaslangic;
        private Button btnTakvimDoldur;
        private Label lblSinavTipi;
        // Uygulamalı ders adları — bu dersler sadece UYGULAMALI tipli salonlara (lab/teknik resim) atanır
        private readonly HashSet<string> _uygulamaliDersler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Bölüm bazlı Hata Oranı (%) — sınava gelmeyecek potansiyel kişi yüzdesi
        private readonly Dictionary<string, double> _bolumHataOranlari = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        // Matris görünüm için salon listesi cache
        private List<Salon> _cachedSalonlar = new List<Salon>();

        // Gözetmen (Araştırma Görevlisi) listesi — Excel'den yüklenir, Ağırlık gridden düzenlenir
        private readonly List<Gozetmen> _gozetmenListesi = new List<Gozetmen>();

        // Matris zoom (Ctrl + fare tekerleği) durumu
        private const float MatrisBazFontBoyutu = 9F;
        private const float MatrisMinFontBoyutu = 6F;
        private const float MatrisMaxFontBoyutu = 16F;
        private const float MatrisSalonBaslikFontBoyutu = 7F; // Salon başlıkları dar sütuna sığsın diye küçük
        private float _matrisFontBoyutu = MatrisBazFontBoyutu;
        private readonly Dictionary<string, int> _matrisBazSutunGenislikleri = new Dictionary<string, int>();
        private int _matrisBazSatirYuksekligi;

        public MainForm()
        {
            InitializeComponent();
            InitializeEvents();
            InitializeDersGrid();
            InitializeUygulamaliGrid();
            InitializeHataOraniGrid();
            InitializeGozetmenGrid();
            HataOraniGridiniGuncelle();

            // Kalıcı hoca eşlemesini yükle (ilk yüklenen sınav listesi Excel'inden kaydedilmiş)
            HocaEslemesiniYukle();

            // Sınav dönemi tipi (Vize/Final) kontrolleri — accordion'dan ÖNCE kurulur ki
            // accordion kurulamasa bile (aşağıdaki catch) kullanıcı tip seçebilsin.
            SinavTipiKontrolleriniKur();

            // Sol paneli açılır/kapanır (accordion) kutucuklara çevir + sağ paneli gizle
            try { AkordeonKur(); } catch { /* accordion kurulamazsa düz yerleşim kalır */ }

            // Tüm başlangıç mantığı form tamamen yüklendikten sonra çalışsın
            this.Shown += MainForm_Shown;
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // İlk açılışta sadece bir kez çalışsın
            this.Shown -= MainForm_Shown;

            VeritabaniniKontrolEt();
            PrepopulateDefaultCalendar();
        }

        // ══════════════════════════════════════════════════════════════
        //  AÇILIR / KAPANIR SOL PANEL (ACCORDION) + SAĞ PANELİ GİZLE
        //  Yer kaplayan sol bölümler, tıklandıkça açılıp kapanan kutucuklara dönüştürülür.
        //  Sağ (sonuç) paneli varsayılan gizli; küçük bir düğmeyle açılıp kapatılır.
        // ══════════════════════════════════════════════════════════════
        private class AkBolum
        {
            public Button Baslik; public Button PopButton; public Panel Icerik;
            public DataGridView Grid; public bool Acik; public string Ad;
        }
        private readonly List<AkBolum> _akordeon = new List<AkBolum>();
        private Button btnSagToggle;
        private Form _acikPopup = null;
        private const int AkordeonUst = 54;

        private const int SolMarj = 13;

        private void AkordeonKur()
        {
            // Durum etiketini sol panele taşı (sağ panel gizliyken de görünsün)
            panelSag.Controls.Remove(lblDurum);
            lblDurum.AutoSize = false;
            lblDurum.Location = new Point(13, 12);
            lblDurum.Size = new Size(520, 34);
            lblDurum.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            if (lblDurum.ForeColor.ToArgb() == Color.Black.ToArgb())
                lblDurum.ForeColor = Color.FromArgb(166, 173, 200);
            panelSol.Controls.Add(lblDurum);
            lblDurum.BringToFront();

            // ── MANUEL DERS EKLEME KALDIRILDI ──
            // Ders girişi YALNIZCA Excel yüklemesiyle yapılır (BtnExcelImport_Click).
            // Ekleme formu kontrolleri gizlenir; kalan kontroller yukarı taşınır.
            foreach (Control gizle in new Control[] { lblSubeKuraliUyari, lblBolum, clbBolum,
                lblSinifSeviyesi, numSinifSeviyesi, lblDersAdi, cmbDersAdi, lblSube, txtSube,
                lblOgrenciSayisi, numOgrenciSayisi, btnDersEkle })
                gizle.Visible = false;

            int dersSatirUst = lblDersBaslik.Bottom + 8;
            btnDersSil.Location = new Point(13, dersSatirUst);
            btnHavuzYonetimi.Location = new Point(btnDersSil.Right + 8, dersSatirUst);
            txtDersAra.Location = new Point(btnHavuzYonetimi.Right + 8, dersSatirUst);
            txtDersAra.Width = Math.Max(120, 505 - txtDersAra.Left);
            int dersGridUst = dersSatirUst + Math.Max(btnDersSil.Height, txtDersAra.Height) + 8;
            dgvDersler.Height += Math.Max(0, dgvDersler.Top - dersGridUst); // boşalan alan grid'e verilir
            dgvDersler.Top = dersGridUst;

            var bolumler = new List<(string Ad, Control[] Kontroller)>
            {
                ("📚 Ders Listesi (Excel'den Yüklenir)", new Control[]{ lblDersBaslik, btnDersSil, btnHavuzYonetimi, txtDersAra, dgvDersler }),
                ("🔬 Uygulamalı Ders Salonu (Lab / Çizim)", new Control[]{ lblOverbooking, txtUygulamaliAra, dgvOverbooking }),
                ("🎯 Hata Oranı (Bölüm Bazlı)", new Control[]{ lblHataOrani, dgvHataOrani }),
                ("⏰ Sınav Takvimi ve Saat Ayarları", new Control[]{ separator, lblZamanBaslik,
                    lblSinavTipi, cmbSinavTipi, dtpSinavBaslangic, btnTakvimDoldur,
                    calendarSinav, chkGunEtkin, flowSaatler }),
                ("🧑‍🏫 Gözetmenler Slotu", new Control[]{ lblGozetmenBaslik, btnGozetmenYukle, dgvGozetmenler }),
            };

            foreach (var bl in bolumler)
            {
                var ctrls = bl.Kontroller.Where(c => c != null).ToList();
                if (ctrls.Count == 0) continue;

                int minTop = ctrls.Min(c => c.Top);
                int maxBot = ctrls.Max(c => c.Bottom);
                var pnl = new Panel
                {
                    Left = 0, Top = 0, Width = 525, Height = Math.Max(maxBot - minTop, 20),
                    Visible = false, BackColor = panelSol.BackColor
                };
                foreach (var c in ctrls)
                {
                    int nt = c.Top - minTop;
                    panelSol.Controls.Remove(c);
                    pnl.Controls.Add(c);
                    c.Top = nt;
                }
                panelSol.Controls.Add(pnl);

                var bol = new AkBolum { Icerik = pnl, Acik = false, Ad = bl.Ad };
                var hdr = new Button
                {
                    Left = 13, Width = 505, Height = 32,
                    Text = "▶  " + bl.Ad, TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(49, 50, 68),
                    ForeColor = Color.FromArgb(205, 214, 244),
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand, UseVisualStyleBackColor = false, Padding = new Padding(6, 0, 0, 0)
                };
                hdr.FlatAppearance.BorderSize = 1;
                hdr.FlatAppearance.BorderColor = Color.FromArgb(69, 71, 90);
                hdr.Click += (s, e) =>
                {
                    bol.Acik = !bol.Acik;
                    hdr.Text = (bol.Acik ? "▼  " : "▶  ") + bol.Ad;
                    AkordeonYerlestir();
                };
                bol.Baslik = hdr;
                panelSol.Controls.Add(hdr);
                hdr.BringToFront();

                // Bölümde grid varsa: başlığı kısalt + sağına ⤢ "ayrı büyük pencerede aç" butonu
                var grid = ctrls.OfType<DataGridView>().FirstOrDefault();
                bol.Grid = grid;
                if (grid != null)
                {
                    hdr.Width = 470;
                    var pop = new Button
                    {
                        Width = 34, Height = 32, Text = "⤢",
                        FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(49, 50, 68),
                        ForeColor = Color.FromArgb(137, 180, 250),
                        Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                        Cursor = Cursors.Hand, UseVisualStyleBackColor = false, TabStop = false
                    };
                    pop.FlatAppearance.BorderSize = 1;
                    pop.FlatAppearance.BorderColor = Color.FromArgb(69, 71, 90);
                    var yerel = bol;
                    pop.Click += (s, e) => SectionPopOut(yerel);
                    bol.PopButton = pop;
                    panelSol.Controls.Add(pop);
                    pop.BringToFront();
                }
                _akordeon.Add(bol);
            }

            // Sağ (sonuç) panelini varsayılan gizle + aç/kapat düğmesi
            panelSag.Visible = false;
            btnSagToggle = new Button
            {
                Left = 545, Top = 12, Width = 250, Height = 32,
                Text = "🖥 Sonuç Panelini Aç / Kapat", TextAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(69, 71, 90),
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            btnSagToggle.FlatAppearance.BorderSize = 0;
            btnSagToggle.Click += (s, e) =>
            {
                panelSag.Visible = !panelSag.Visible;
                // Sonuç panelini açınca form dar kalmışsa genişlet
                if (panelSag.Visible && ClientSize.Width < 1300)
                    ClientSize = new Size(1300, ClientSize.Height);
            };
            panelSol.Controls.Add(btnSagToggle);
            btnSagToggle.BringToFront();

            SagSutunuGrupla();

            // Pencere sabit boyutlu olduğu için açılan bölümler alta taşabilir:
            // sol panel kaydırılabilir yapılır, böylece hiçbir kontrol erişilemez kalmaz.
            panelSol.AutoScroll = true;

            AkordeonYerlestir();
        }

        // ── Bir accordion bölümünü ayrı, boyutlandırılabilir BÜYÜK pencerede açar ──
        // Grid pencereyi doldurur (çok daha fazla satır görünür); üst kontroller şerit halinde.
        // Pencere kapanınca tüm kontroller accordion paneline eski haliyle döner.
        private void SectionPopOut(AkBolum bol)
        {
            if (_acikPopup != null || bol?.Grid == null) return;
            var grid = bol.Grid;

            var hepsi = bol.Icerik.Controls.Cast<Control>().ToList();
            var ustler = hepsi.Where(c => c != grid).ToList();
            // orijinal durumu yakala (kontrol, konum, boyut, dock)
            var yakalanan = hepsi.Select(c => (Ctrl: c, Loc: c.Location, Sz: c.Size, Dk: c.Dock)).ToList();

            var form = new Form
            {
                Text = "🔎 " + bol.Ad,
                ClientSize = new Size(1000, 640),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.Sizable,
                MaximizeBox = true,
                MinimizeBox = false,
                MinimumSize = new Size(560, 360),
                BackColor = Color.FromArgb(30, 30, 46),
                ForeColor = Color.FromArgb(205, 214, 244),
                Font = new Font("Segoe UI", 9.5F),
                ShowInTaskbar = false
            };

            var pnlUst = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(36, 36, 58),
                Padding = new Padding(10, 8, 10, 8)
            };

            foreach (var c in ustler)
            {
                bol.Icerik.Controls.Remove(c);
                c.Dock = DockStyle.None;
                c.Margin = new Padding(6, 6, 6, 6);
                pnlUst.Controls.Add(c);
            }

            bol.Icerik.Controls.Remove(grid);
            grid.Dock = DockStyle.Fill;
            form.Controls.Add(grid);   // Fill önce eklenir
            form.Controls.Add(pnlUst); // Top sonra

            form.FormClosed += (s, e) =>
            {
                foreach (var t in yakalanan)
                {
                    t.Ctrl.Parent?.Controls.Remove(t.Ctrl);
                    t.Ctrl.Dock = t.Dk;
                    t.Ctrl.Location = t.Loc;
                    t.Ctrl.Size = t.Sz;
                    bol.Icerik.Controls.Add(t.Ctrl);
                }
                _acikPopup = null;
                AkordeonYerlestir();
            };

            _acikPopup = form;
            form.ShowDialog(this);
        }

        // ══════════════════════════════════════════════════════════════
        //  SAĞ SÜTUN — ADIM GRUPLARI
        //  İş akışı butonları başlıklı gruplara (kutulara) toplanır ve yukarıdan
        //  aşağıya otomatik dizilir; buton eklendiğinde sabit konumlar kaymaz.
        // ══════════════════════════════════════════════════════════════
        private Panel _sagSonGrup;
        private Button btnDerslikYukle;

        /// <summary>
        /// Derslik Excel'ini okur, değişiklik özetini gösterir ve onaylanırsa veritabanındaki
        /// salon listesini TAMAMEN bununla değiştirir (listede olmayan eski salonlar silinir).
        /// </summary>
        private void BtnDerslikYukle_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                Title = "Derslik Listesi Excel'ini Seç"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var okuma = DerslikListesiOkuyucu.Oku(dlg.FileName);
                    if (okuma.Salonlar.Count == 0)
                    {
                        MessageBox.Show("Dosyadan derslik okunamadı.\n\n" + string.Join("\n", okuma.Uyarilar),
                            "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Mevcut liste ile karşılaştır
                    List<Salon> eski;
                    try { eski = DatabaseHelper.SalonlariGetir(); } catch { eski = new List<Salon>(); }
                    var eskiMap = eski.GroupBy(s => s.SalonKodu.Trim(), StringComparer.OrdinalIgnoreCase)
                                      .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                    var yeniKodlar = new HashSet<string>(okuma.Salonlar.Select(s => s.SalonKodu), StringComparer.OrdinalIgnoreCase);

                    var eklenen = okuma.Salonlar.Where(s => !eskiMap.ContainsKey(s.SalonKodu)).Select(s => s.SalonKodu).ToList();
                    var silinen = eskiMap.Keys.Where(k => !yeniKodlar.Contains(k)).OrderBy(k => k).ToList();
                    var degisen = okuma.Salonlar
                        .Where(s => eskiMap.TryGetValue(s.SalonKodu, out var o) &&
                                    (o.Kapasite != s.Kapasite || o.UygulamaliMi != s.UygulamaliMi))
                        .Select(s =>
                        {
                            var o = eskiMap[s.SalonKodu];
                            return $"{s.SalonKodu}: {o.Kapasite}{(o.UygulamaliMi ? " (uyg.)" : "")} → {s.Kapasite}{(s.UygulamaliMi ? " (uyg.)" : "")}";
                        }).ToList();

                    int uygSayi = okuma.Salonlar.Count(s => s.UygulamaliMi);
                    string Liste(List<string> l) => l.Count == 0 ? "—" : string.Join(", ", l.Take(30)) + (l.Count > 30 ? " ..." : "");

                    string ozet =
                        $"Dosyada {okuma.Salonlar.Count} derslik bulundu " +
                        $"({okuma.Salonlar.Count - uygSayi} normal, {uygSayi} uygulamalı/lab).\n" +
                        $"Toplam sınav kapasitesi: {okuma.Salonlar.Sum(s => s.Kapasite)}\n\n" +
                        $"➕ Eklenecek ({eklenen.Count}): {Liste(eklenen)}\n" +
                        $"➖ Silinecek ({silinen.Count}): {Liste(silinen)}\n" +
                        $"✏ Değişecek ({degisen.Count}):\n   {(degisen.Count == 0 ? "—" : string.Join("\n   ", degisen.Take(20)))}\n" +
                        (okuma.Uyarilar.Count > 0 ? $"\n⚠ Uyarılar:\n{string.Join("\n", okuma.Uyarilar.Take(10))}\n" : "") +
                        "\nMevcut salon listesi bu listeyle DEĞİŞTİRİLSİN mi?";

                    if (MessageBox.Show(ozet, "Derslik Listesi — Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                        != DialogResult.Yes) return;

                    DatabaseHelper.SalonlariKaydet(okuma.Salonlar, oncekileriSil: true);
                    _cachedSalonlar = DatabaseHelper.SalonlariGetir();
                    VeritabaniniKontrolEt();

                    // Salonlar değişti → önceki salon/gözetmen sonuçları artık geçersiz
                    btnSalonExport.Enabled = false; btnGozetmenAta.Enabled = false;
                    btnMatrisExport.Enabled = false; btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                    lblDurum.Text = $"✅ Derslik listesi güncellendi: {okuma.Salonlar.Count} derslik. " +
                                    "Salon ataması yaptıysanız 'Salonları Ata'yı yeniden çalıştırın.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Derslik listesi yüklenemedi:\n{ex.Message}", "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SagSutunuGrupla()
        {
            const int SagSol = 545, GrupGenislik = 250, IcBosluk = 6, ButonYuk = 34, GrupArasi = 10;

            // Derslik listesi yükleme — iş akışından bağımsız, her zaman aktif
            btnDerslikYukle = new Button
            {
                Text = "🏫 Derslik Listesi Yükle",
                BackColor = Color.FromArgb(148, 226, 213), ForeColor = Color.FromArgb(30, 30, 46),
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            btnDerslikYukle.FlatAppearance.BorderSize = 0;
            btnDerslikYukle.Click += BtnDerslikYukle_Click;

            var gruplar = new (string Baslik, Color Renk, Button[][] Satirlar)[]
            {
                ("0 · DERSLİKLER", Color.FromArgb(148, 226, 213), new[]
                {
                    new[] { btnDerslikYukle },
                }),
                ("1 · SINAV LİSTESİ VE ŞABLON", Color.FromArgb(249, 226, 175), new[]
                {
                    new[] { btnExcelImport },
                    new[] { btnSablonOlustur, btnOtomatikDagit },
                }),
                ("2 · SALON ATAMASI", Color.FromArgb(137, 180, 250), new[]
                {
                    new[] { btnSablonYukle }, new[] { btnSalonAta }, new[] { btnSalonExport },
                }),
                ("3 · GÖZETMEN ATAMASI", Color.FromArgb(198, 160, 246), new[]
                {
                    new[] { btnGozetmenDosyaYukle }, new[] { btnGozetmenAta },
                }),
                ("4 · KONTROL VE FİNAL", Color.FromArgb(166, 227, 161), new[]
                {
                    new[] { btnMatrisExport }, new[] { btnGozetmenliYukle }, new[] { btnFinalExport },
                }),
            };

            int y = btnSagToggle.Bottom + 12;
            foreach (var g in gruplar)
            {
                var grup = new Panel
                {
                    Left = SagSol, Top = y, Width = GrupGenislik,
                    BackColor = Color.FromArgb(40, 41, 60)
                };
                // Sol kenarda grup rengini taşıyan ince şerit
                grup.Controls.Add(new Panel { Left = 0, Top = 0, Width = 3, Dock = DockStyle.Left, BackColor = g.Renk });

                var lbl = new Label
                {
                    Text = g.Baslik, AutoSize = false,
                    Left = 10, Top = 4, Width = GrupGenislik - 14, Height = 20,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    ForeColor = g.Renk, BackColor = Color.Transparent
                };
                grup.Controls.Add(lbl);

                int iy = lbl.Bottom + 4;
                int icSol = 10, icGenislik = GrupGenislik - icSol - IcBosluk;
                foreach (var satir in g.Satirlar)
                {
                    var butonlar = satir.Where(b => b != null).ToArray();
                    if (butonlar.Length == 0) continue;
                    // Yan yana butonlar iki satır metin taşır → biraz daha yüksek
                    int yuk = butonlar.Length > 1 ? ButonYuk + 12 : ButonYuk;
                    int gen = (icGenislik - (butonlar.Length - 1) * IcBosluk) / butonlar.Length;
                    for (int i = 0; i < butonlar.Length; i++)
                    {
                        var b = butonlar[i];
                        b.Parent?.Controls.Remove(b);
                        b.SetBounds(icSol + i * (gen + IcBosluk), iy, gen, yuk);
                        grup.Controls.Add(b);
                    }
                    iy += yuk + IcBosluk;
                }

                grup.Height = iy + 2;
                panelSol.Controls.Add(grup);
                grup.BringToFront();
                _sagSonGrup = grup;
                y = grup.Bottom + GrupArasi;
            }
        }

        private void AkordeonYerlestir()
        {
            // panelSol kaydırılabilir (AutoScroll) olduğu için Top/Left değerleri
            // kaydırma ofsetiyle birlikte yazılmalıdır; aksi halde kullanıcı aşağı
            // kaydırdıktan sonra bir bölüm açınca kontroller yerinden zıplar.
            int sy = panelSol.AutoScrollPosition.Y;   // 0 veya negatif

            int y = AkordeonUst;
            foreach (var b in _akordeon)
            {
                b.Baslik.Left = SolMarj;
                b.Baslik.Top = y + sy;
                if (b.PopButton != null)
                {
                    b.PopButton.Top = y + sy;
                    b.PopButton.Left = b.Baslik.Right + 3;
                }
                y += b.Baslik.Height + 4;
                if (b.Acik)
                {
                    b.Icerik.Top = y + sy;
                    b.Icerik.Visible = true;
                    y += b.Icerik.Height + 8;
                }
                else b.Icerik.Visible = false;
            }

            // Reset/temizle her iki sütunun da altına (tam genişlik).
            // Sağ sütun gruplandıysa son grubun altı, değilse son adım butonunun altı esas alınır.
            int butonSutunuAlt = (_sagSonGrup != null ? _sagSonGrup.Bottom : btnFinalExport.Bottom) - sy;   // mantıksal (kaydırmasız) koordinat
            int altSinir = Math.Max(y + 8, butonSutunuAlt + 14);
            lblDbDurum.Top = altSinir + sy;
            btnVeritabaniSifirla.Top = altSinir + 18 + sy;
            btnTemizle.Top = btnVeritabaniSifirla.Bottom + 6;
        }

        private void InitializeEvents()
        {
            // Manuel ders ekleme kaldırıldı — ders girişi yalnızca Excel importu (btnExcelImport)
            btnDersSil.Click += BtnDersSil_Click;
            btnExcelImport.Click += BtnExcelImport_Click;          // ADIM 1: Ders Listesi Yükle
            btnSablonOlustur.Click += BtnSablonOlustur_Click;      // ADIM 2: Şablon Excel Oluştur (boş)
            btnOtomatikDagit.Click += BtnOtomatikDagit_Click;      // ADIM 2+: Otomatik dağıtımlı şablon
            btnSablonYukle.Click += BtnSablonYukle_Click;          // Salon Ataması İçin Dosya Yükle
            btnSalonAta.Click += BtnSalonAta_Click;                // Salonları Ata (sadece atama)
            btnSalonExport.Click += BtnSalonExport_Click;          // Salon Atanmış Dosya Çıkart
            btnGozetmenDosyaYukle.Click += BtnGozetmenDosyaYukle_Click; // Gözetmenler için Dosya Yükle
            btnTemizle.Click += BtnTemizle_Click;
            btnMatrisExport.Click += BtnMatrisExport_Click;
            btnGozetmenliYukle.Click += BtnGozetmenliYukle_Click;   // Adım 7: düzenlenmiş dosyayı geri yükle
            btnFinalExport.Click += BtnFinalExport_Click;
            btnVeritabaniSifirla.Click += BtnVeritabaniSifirla_Click;

            // ── KALDIRILAN MODÜLLER ──
            // Havuz + Ortak Ders Yönetimi: sınav listesi yüklendiği anda kullanılabilir.
            // Havuz → seçilen şubeler salon atamasında tek salonda birleştirilir.
            // Ortak  → ortak dersler otomatik değil, kullanıcı tarafından MANUEL seçilir.
            btnHavuzYonetimi.Visible = true;
            btnHavuzYonetimi.Text = "🔗 Havuz / Ortak";
            btnHavuzYonetimi.Click += BtnHavuzYonetimi_Click;
            cmbSablonMod.Visible = false;

            // ── ADIM KİLİTLERİ (5 adımlı Excel gidiş-dönüş akışı) ──
            // 1 açık; 2 liste yüklenince; 3 her zaman (hazır doldurulmuş dosya için); 4 şablon geri yüklenince.
            btnSablonOlustur.Enabled = false;   // Şablon Oluştur — Ders Listesi'nden sonra
            btnOtomatikDagit.Enabled = false;   // Otomatik Dağıt — Ders Listesi'nden sonra
            btnSalonAta.Enabled = false;        // Salonları Ata — Salon Ataması İçin Dosya Yükle'den sonra
            btnSalonExport.Enabled = false;     // Salon Atanmış Dosya Çıkart — Salonları Ata'dan sonra
            btnGozetmenAta.Enabled = false;     // Gözetmen Ata — Gözetmenler için Dosya Yükle'den sonra
            btnMatrisExport.Enabled = false;    // Gözetmenli Taslak Çıkart — Gözetmen Ata'dan sonra
            btnGozetmenliYukle.Enabled = false; // Düzenlenmiş Dosyayı Yükle — Taslak Çıkart'tan sonra
            btnFinalExport.Enabled = false;     // Final Programı Çıkart — Düzenlenmiş Dosyayı Yükle'den sonra

            dgvDersler.CellEndEdit += DgvDersler_CellEndEdit;
            txtDersAra.TextChanged += (s, e) => DersGridiGuncelle();

            // Takvim ve Uygulamalı Ders olayları
            calendarSinav.DateSelected += CalendarSinav_DateSelected;
            chkGunEtkin.CheckedChanged += ChkGunEtkin_CheckedChanged;
            dgvOverbooking.CurrentCellDirtyStateChanged += DgvUygulamali_CurrentCellDirtyStateChanged;
            dgvOverbooking.CellValueChanged += DgvUygulamali_CellValueChanged;
            txtUygulamaliAra.TextChanged += (s, e) => UygulamaliDersGridiniGuncelle();

            // Hata Oranı grid olayları
            dgvHataOrani.CellEndEdit += DgvHataOrani_CellEndEdit;

            // Gözetmen Atama Modülü olayları
            btnGozetmenYukle.Click += BtnGozetmenYukle_Click;
            btnGozetmenAta.Click += BtnGozetmenAta_Click;
            dgvGozetmenler.CellEndEdit += DgvGozetmenler_CellEndEdit;

            // Matris görünümünde salon hücrelerinin elle düzenlenmesi
            dgvMatris.CellEndEdit += DgvMatris_CellEndEdit;

            // Matris zoom: Ctrl + fare tekerleği ile font/hücre boyutu değiştirme
            dgvMatris.MouseWheel += DgvMatris_MouseWheel;

            // Matris filtre olayları
            cmbFiltreBolum.SelectedIndexChanged += (s, e) => MatrisFiltreUygula();
            cmbFiltreSinif.SelectedIndexChanged += (s, e) => MatrisFiltreUygula();

            // ── Şablon grid olayları (Excel'den gelen matris üzerinde düzenleme) ──
            dgvSablon.CellValueChanged += DgvSablon_CellValueChanged;
            dgvSablon.CellEndEdit += DgvSablon_CellEndEdit;
            dgvSablon.DataError += (s, e) => e.ThrowException = false; // ComboBox değer uyuşmazlıklarını sessizce yut
            dgvSablon.CellPainting += DgvSablon_CellPainting;
            dgvSablon.Paint += DgvSablon_Paint;
            dgvSablon.Scroll += (s, e) => dgvSablon.Invalidate();
            dgvSablon.ColumnWidthChanged += (s, e) => dgvSablon.Invalidate();
            dgvSablon.Resize += (s, e) => dgvSablon.Invalidate();

            // Excel rahatlığı: yazarak ders arama (autocomplete), Delete ile ders silme,
            // Ctrl + fare tekerleği ile zoom (matris görünümündeki ile aynı)
            dgvSablon.EditingControlShowing += DgvSablon_EditingControlShowing;
            dgvSablon.KeyDown += DgvSablon_KeyDown;
            dgvSablon.MouseWheel += DgvSablon_MouseWheel;
        }

        private void InitializeDersGrid()
        {
            dgvDersler.Columns.Clear();
            dgvDersler.AutoGenerateColumns = false;

            dgvDersler.Columns.Add(new DataGridViewTextBoxColumn { Name = "DersAdi", HeaderText = "Ders Adı ✏", ReadOnly = false, FillWeight = 25 });
            dgvDersler.Columns.Add(new DataGridViewTextBoxColumn { Name = "BolumAdi", HeaderText = "Bölüm ✏", ReadOnly = false, FillWeight = 22 });
            dgvDersler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sinif", HeaderText = "Sınıf ✏", ReadOnly = false, FillWeight = 8 });
            dgvDersler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sube", HeaderText = "Şube ✏", ReadOnly = false, FillWeight = 8 });
            dgvDersler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kontenjan", HeaderText = "Kontenjan ✏", ReadOnly = false, FillWeight = 12 });

            foreach (DataGridViewColumn col in dgvDersler.Columns)
            {
                col.DefaultCellStyle.BackColor = Color.FromArgb(60, 62, 85);
            }
        }

        /// <summary>
        /// Veritabanı bağlantısını SADECE Salonlar için test eder.
        /// Dersler artık SQL'den çekilmez, tek kaynak Excel'dir.
        /// </summary>
        private void VeritabaniniKontrolEt()
        {
            try
            {
                if (DatabaseHelper.BaglantiyiTest())
                {
                    var salonlar = DatabaseHelper.SalonlariGetir();
                    _cachedSalonlar = salonlar;

                    if (salonlar.Count == 0)
                    {
                        // Yeni bir bilgisayarda ilk açılış: veritabanı oluştu ama salon listesi boş.
                        lblDbDurum.Text = "⚠️ Veritabanı hazır ama salon kaydı yok — Baslangic\\Salonlar.csv dosyasını doldurun";
                        lblDbDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    }
                    else
                    {
                        lblDbDurum.Text = $"✅ Veritabanı bağlı — {salonlar.Count} salon bulundu (Salonlar için)";
                        lblDbDurum.ForeColor = Color.FromArgb(166, 227, 161);
                    }
                }
                else { lblDbDurum.Text = "❌ Veritabanı bağlantısı başarısız! (Salonlar alınamayacak)"; lblDbDurum.ForeColor = Color.FromArgb(243, 139, 168); }
            }
            catch (Exception ex) { lblDbDurum.Text = $"❌ Hata: {ex.Message}"; lblDbDurum.ForeColor = Color.FromArgb(243, 139, 168); }
        }

        // =============================================
        // DERS EKLEME / SİLME / İNLİNE DÜZENLEME
        // (Tümü bellek içi — SQL'e hiçbir ders kaydedilmez)
        // =============================================

        // NOT: BtnDersEkle_Click (manuel ders ekleme) KALDIRILDI.
        // Ders girişi yalnızca Excel importu üzerinden yapılır (BtnExcelImport_Click).

        /// <summary>
        /// Bölüm CheckedListBox'ındaki tüm işaretleri kaldırır.
        /// </summary>
        private void BolumSecimleriniTemizle()
        {
            for (int i = 0; i < clbBolum.Items.Count; i++)
                clbBolum.SetItemChecked(i, false);
        }

        /// <summary>
        /// Seçili satırı listeden siler (bellek içi).
        /// </summary>
        private void BtnDersSil_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvDersler.CurrentRow == null || !(dgvDersler.CurrentRow.Tag is Ders silinecek))
                { MessageBox.Show("Silmek için bir ders satırı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                var onay = MessageBox.Show($"'{silinecek.DersAdi} — Şube {silinecek.Sube}' silinecek. Emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (onay == DialogResult.Yes)
                {
                    _dersListesi.Remove(silinecek); // Tag referansıyla — filtre altında index güvenilmez
                    DersGridiGuncelle();
                    UygulamaliDersGridiniGuncelle();
                    lblDurum.Text = $"🗑 '{silinecek.DersAdi}' listeden silindi."; lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Ders silinirken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>
        /// Grid'de yapılan tüm inline hücre değişiklikleri bellek içi listeye yansıtılır.
        /// </summary>
        private void DgvDersler_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating) return;
            if (e.RowIndex < 0) return;

            var r = dgvDersler.Rows[e.RowIndex];
            var dersAdiObj = r.Cells["DersAdi"].Value;
            var bolumAdiObj = r.Cells["BolumAdi"].Value;
            var sinifObj = r.Cells["Sinif"].Value;
            var subeObj = r.Cells["Sube"].Value;
            var kontenjanObj = r.Cells["Kontenjan"].Value;

            string dersAdi = dersAdiObj?.ToString()?.Trim()?.ToUpperInvariant() ?? "";
            string bolumAdi = bolumAdiObj?.ToString()?.Trim() ?? "";
            string sube = subeObj?.ToString()?.Trim()?.ToUpperInvariant() ?? "A";
            if (string.IsNullOrEmpty(sube)) sube = "A";

            int sinif = 1;
            if (sinifObj != null)
            {
                int.TryParse(sinifObj.ToString(), out sinif);
                if (sinif < 1) sinif = 1;
                if (sinif > 4) sinif = 4;
            }

            int kontenjan = 0;
            if (kontenjanObj != null)
            {
                int.TryParse(kontenjanObj.ToString(), out kontenjan);
                if (kontenjan < 0) kontenjan = 0;
            }

            // Yeni satır ekleme (Tag'siz satır = grid'in altına elle girilen yeni kayıt)
            if (!(r.Tag is Ders mevcutDers))
            {
                if (string.IsNullOrEmpty(dersAdi) || string.IsNullOrEmpty(bolumAdi))
                    return; // Yeterli bilgi girilmedi

                var yeniDers = new Ders
                {
                    DersAdi = dersAdi,
                    BolumAdi = bolumAdi,
                    SinifSeviyesi = sinif,
                    Sube = sube,
                    Kontenjan = kontenjan
                };
                _dersListesi.Add(yeniDers);
                r.Tag = yeniDers; // satır ↔ ders eşleşmesi

                _isUpdating = true;
                r.Cells["DersAdi"].Value = dersAdi;
                r.Cells["BolumAdi"].Value = bolumAdi;
                r.Cells["Sinif"].Value = sinif;
                r.Cells["Sube"].Value = sube;
                r.Cells["Kontenjan"].Value = kontenjan;
                _isUpdating = false;

                UygulamaliDersGridiniGuncelle();
                lblDurum.Text = $"➕ '{dersAdi}' başarıyla listeye eklendi.";
                lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
            }
            else
            {
                // Mevcut satır düzenleme — Tag'deki referans üzerinden (filtre altında index güvenilmez)
                var ders = mevcutDers;

                if (ders.DersAdi == dersAdi &&
                    ders.BolumAdi == bolumAdi &&
                    ders.SinifSeviyesi == sinif &&
                    ders.Sube == sube &&
                    ders.Kontenjan == kontenjan)
                {
                    return; // Değişiklik yok
                }

                if (string.IsNullOrEmpty(dersAdi) || string.IsNullOrEmpty(bolumAdi))
                {
                    MessageBox.Show("Ders Adı ve Bölüm boş bırakılamaz!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DersGridiGuncelle();
                    return;
                }

                ders.DersAdi = dersAdi;
                ders.BolumAdi = bolumAdi;
                ders.SinifSeviyesi = sinif;
                ders.Sube = sube;
                ders.Kontenjan = kontenjan;

                _isUpdating = true;
                r.Cells["DersAdi"].Value = ders.DersAdi;
                r.Cells["BolumAdi"].Value = ders.BolumAdi;
                r.Cells["Sinif"].Value = ders.SinifSeviyesi;
                r.Cells["Sube"].Value = ders.Sube;
                r.Cells["Kontenjan"].Value = ders.Kontenjan;
                _isUpdating = false;

                UygulamaliDersGridiniGuncelle();
                lblDurum.Text = $"✏ '{ders.DersAdi}' güncellendi.";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
            }
        }

        private void DersGridiGuncelle()
        {
            _isUpdating = true;
            try
            {
                // Aktif düzenlemeyi iptal et — Rows.Clear() sırasında CellEndEdit patlamasını önler
                dgvDersler.CancelEdit();
                if (dgvDersler.IsCurrentCellInEditMode)
                    dgvDersler.EndEdit();
                dgvDersler.CurrentCell = null;

                dgvDersler.SuspendLayout();
                dgvDersler.Rows.Clear();

                // Arama filtresi (Türkçe karakterlere duyarlı, büyük/küçük harf bağımsız)
                string filtre = txtDersAra.Text?.Trim();
                var gosterilecekDersler = _dersListesi;
                if (!string.IsNullOrEmpty(filtre))
                {
                    var trCompare = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
                    gosterilecekDersler = _dersListesi
                        .Where(d => trCompare.IndexOf(d.DersAdi ?? "", filtre, System.Globalization.CompareOptions.IgnoreCase) >= 0
                                 || trCompare.IndexOf(d.BolumAdi ?? "", filtre, System.Globalization.CompareOptions.IgnoreCase) >= 0)
                        .ToList();
                }

                foreach (var ders in gosterilecekDersler)
                {
                    int rowIdx = dgvDersler.Rows.Add(ders.DersAdi, ders.BolumAdi, ders.SinifSeviyesi, ders.Sube, ders.Kontenjan);
                    // Satır ↔ ders eşleşmesi Tag ile yapılır — filtre aktifken satır index'i güvenilmezdir
                    dgvDersler.Rows[rowIdx].Tag = ders;
                }

                dgvDersler.ResumeLayout();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // =============================================
        // EXCEL'DEN DERS PROGRAMI YÜKLEME (IMPORT)
        // Tek kaynak — SQL'e hiçbir ders kaydedilmez
        // =============================================

        // ─────────────────────────────────────────────────────────────
        //  ADIM 1 — DERS LİSTESİ YÜKLE
        //  "Sınav Listesi" ham Excel'inden TÜM dersleri (Normal + İÖ) çeker.
        //  Salon/takvim burada oluşmaz; yalnızca ders havuzu belleğe alınır.
        // ─────────────────────────────────────────────────────────────
        private void BtnExcelImport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*";
                    dlg.Title = "Sınav Listesi Excel'ini Seçin (ham ders listesi)";
                    dlg.FilterIndex = 1;

                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Ders listesi okunuyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    var okuma = SinavListesiOkuyucu.Oku(dlg.FileName);

                    if (okuma.Dersler.Count == 0)
                    {
                        string uyariMesaji = "Excel'den hiç ders okunamadı.\n" +
                            "(Bölüm, Dersin Kodu, Ders Adı, Şube, Öğrenci Sayısı sütunları beklenir.)";
                        if (okuma.Uyarilar.Count > 0)
                            uyariMesaji += $"\n\n{string.Join("\n", okuma.Uyarilar.Take(15))}";
                        MessageBox.Show(uyariMesaji, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblDurum.Text = "❌ Hiç ders yüklenemedi.";
                        lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                        return;
                    }

                    var bolumOzet = okuma.Dersler
                        .GroupBy(d => d.BolumAdi)
                        .Select(g => $"  • {g.Key}: {g.Select(x => x.DersKodu).Distinct().Count()} ders kodu, {g.Count()} şube")
                        .OrderBy(s => s)
                        .ToList();

                    string ozet = $"Ders listesi okundu:\n\n" +
                                  $"{okuma.Dersler.Count} şube, " +
                                  $"{okuma.Dersler.Select(d => d.DersKodu).Distinct().Count()} farklı ders kodu:\n\n" +
                                  $"{string.Join("\n", bolumOzet)}";
                    if (okuma.Uyarilar.Count > 0)
                        ozet += $"\n\n⚠ {okuma.Uyarilar.Count} satır atlandı/uyarı.";

                    var onay = MessageBox.Show(
                        $"{ozet}\n\nBu ders listesi yüklensin mi?\n(Mevcut liste ve varsa yerleşim temizlenecektir)",
                        "Ders Listesi Yükleme Onayı",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (onay != DialogResult.Yes) return;

                    // ── Ders havuzunu belleğe al ──
                    _dersListesi.Clear();
                    _dersListesi.AddRange(okuma.Dersler);

                    // Hoca bilgilerini bu Excel'den çıkar ve KALICI dosyaya kaydet
                    // (Sınav Programı Formu'nun Öğretim Elemanı sütunu buradan beslenir)
                    HocaEslemesiniGuncelleVeKaydet();

                    // Downstream durumu sıfırla (yeni liste → yeni şablon gerekecek)
                    _takvimSlotlari.Clear();
                    _sablonSonuclari = null;
                    _sablonDuzenlendi = false;
                    _sablonIntNotlari.Clear();
                    _sonSonuclar = null;

                    DersGridiGuncelle();
                    UygulamaliDersGridiniGuncelle();
                    HataOraniGridiniGuncelle();

                    // Adım kilitleri: 2 ve 2+ açılır; 4/5 kapalı
                    btnSablonOlustur.Enabled = true;
                    btnOtomatikDagit.Enabled = true;
                    btnSalonAta.Enabled = false;
                    btnGozetmenAta.Enabled = false;
                    btnMatrisExport.Enabled = false; btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                    lblDurum.Text = $"✅ {okuma.Dersler.Count} şube yüklendi. '2) Şablon Oluştur' veya '2+) Otomatik Dağıt'a basın.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (okuma.Uyarilar.Count > 0)
                    {
                        var detayIste = MessageBox.Show(
                            $"{okuma.Uyarilar.Count} satır atlandı/uyarıldı.\n\nDetayları görmek ister misiniz?",
                            "Yükleme Notları", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (detayIste == DialogResult.Yes)
                            AtlananSatirlariGoster(okuma.Uyarilar);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ders listesi yükleme hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Ders listesi yüklemesi başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  ADIM 2 — ŞABLON EXCEL OLUŞTUR
        //  Yüklenen ders havuzunu "örnek dağıtım" formatındaki bir .xlsx'e yazar;
        //  "Çekilmiş Dersler" sayfası kod-ilk-rakamı gruplarına göre doldurulur.
        //  Kullanıcı bu dosyayı indirip "Sınav Haftası Dağıtım Şablonu"nu elle doldurur.
        // ─────────────────────────────────────────────────────────────
        private void BtnSablonOlustur_Click(object sender, EventArgs e)
        {
            try
            {
                if (_dersListesi.Count == 0)
                {
                    MessageBox.Show("Önce '1) Ders Listesi Yükle' ile ders listesini yükleyin!",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_takvimSlotlari.Count == 0)
                {
                    MessageBox.Show("Takvimde hiç sınav günü seçili değil!\n" +
                        "Sağdaki 'Sınav Takvimi' panelinden en az bir gün seçin.",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx";
                    dlg.Title = "Çekilmiş Dersler Şablonunu Kaydet";
                    dlg.FileName = $"Cekilmis_Dersler_Dagitim_{DateTime.Now:yyyyMMdd}.xlsx";

                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Şablon Excel'i oluşturuluyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    CekilmisDerslerExportHelper.Olustur(_dersListesi, _takvimSlotlari, dlg.FileName, out var uyarilar);

                    lblDurum.Text = "✅ Şablon oluşturuldu. İndirip 'Sınav Haftası Dağıtım Şablonu'nu doldurun.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    string mesaj = "Şablon Excel'i oluşturuldu:\n\n" + dlg.FileName +
                        $"\n\n• 'Çekilmiş Dersler': kod grupları 0–4 + altında 'Ortak Dersler' listesi.\n" +
                        $"• 'Sınav Haftası Dağıtım Şablonu': {_takvimSlotlari.Count} sınav günü tarihleriyle hazırlandı.\n" +
                        $"• Her seçilen gün için ayrı 'Sınav Günü' sayfası oluşturuldu.\n" +
                        "• Sınavları şablona elle yerleştirip '3) Şablonu Yükle' ile geri yükleyin.";
                    if (uyarilar != null && uyarilar.Count > 0)
                        mesaj += $"\n\n⚠ {uyarilar.Count} ders kapsam dışı bırakıldı (detay için not raporu).";

                    mesaj += "\n\nDosya şimdi açılsın mı?";

                    if (MessageBox.Show(mesaj, "Şablon Oluşturuldu",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dlg.FileName,
                                UseShellExecute = true
                            });
                        }
                        catch { /* açma başarısız olursa sessiz geç */ }
                    }

                    if (uyarilar != null && uyarilar.Count > 0)
                        AtlananSatirlariGoster(uyarilar);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon oluşturma hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Şablon oluşturulamadı!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  ADIM 2+ — ŞABLON OLUŞTUR + OTOMATİK DAĞIT
        //  Dersleri çeker VE kurallara göre 'Sınav Haftası Dağıtım Şablonu'na dağıtır.
        //  Kullanıcı beğenmezse Excel'i indirip elle düzenleyip '3) Şablonu Yükle' ile geri verir.
        // ─────────────────────────────────────────────────────────────
        private void BtnOtomatikDagit_Click(object sender, EventArgs e)
        {
            try
            {
                if (_dersListesi.Count == 0)
                {
                    MessageBox.Show("Önce '1) Ders Listesi Yükle' ile ders listesini yükleyin!",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_takvimSlotlari.Count == 0)
                {
                    MessageBox.Show("Takvimde hiç sınav günü seçili değil!\n" +
                        "Sağdaki 'Sınav Takvimi' panelinden en az bir gün seçin.",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Normal salon kapasitesi (Kural 2 için) — DB'den
                List<Salon> salonlar;
                try { salonlar = DatabaseHelper.SalonlariGetir(); _cachedSalonlar = salonlar; }
                catch (Exception dbEx)
                {
                    MessageBox.Show($"Salon bilgileri alınamadı (Kural 2 kapasite kontrolü için gerekli):\n{dbEx.Message}",
                        "DB Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                int normalKapasite = salonlar.Where(s => !s.UygulamaliMi).Sum(s => s.Kapasite);

                lblDurum.Text = "⏳ Dersler kurallara göre dağıtılıyor...";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                Application.DoEvents();

                var algo = new SablonDagitimAlgoritmasi();
                // Ortak dersler: kullanıcı manuel seçim yaptıysa SADECE o liste; yoksa otomatik tespit.
                // HATA ORANI: grid'deki bölüm oranları şablona uygulanır — şablon efektif
                // kontenjanla kurulur, salon dağıtımı da bu şablonu esas alır.
                var dagitim = algo.Dagit(_dersListesi, _takvimSlotlari, normalKapasite,
                                         _ortakSecimYapildi ? _manuelOrtakKodlar : null,
                                         HataOranlariniOku());

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx";
                    dlg.Title = "Otomatik Dağıtılmış Şablonu Kaydet";
                    dlg.FileName = $"Otomatik_Dagitim_{DateTime.Now:yyyyMMdd}.xlsx";

                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    CekilmisDerslerExportHelper.Olustur(_dersListesi, _takvimSlotlari, dlg.FileName, out var uyarilar, dagitim);

                    var tumUyarilar = new List<string>();
                    if (algo.Uyarilar != null) tumUyarilar.AddRange(algo.Uyarilar);
                    if (uyarilar != null) tumUyarilar.AddRange(uyarilar);

                    int yerlesen = dagitim.Select(a => (a.BolumAdi, a.DersAdi, a.Gun, a.Saat)).Distinct().Count();

                    lblDurum.Text = $"✅ Otomatik dağıtım tamam. İndirip kontrol edin, gerekirse elle düzenleyip '3) Şablonu Yükle'.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    string mesaj = "Otomatik dağıtılmış şablon oluşturuldu:\n\n" + dlg.FileName +
                        $"\n\n• {yerlesen} ders slotu 'Sınav Haftası Dağıtım Şablonu'na kurallara göre yerleştirildi.\n" +
                        $"• Normal salon kapasitesi: {normalKapasite} (Kural 2 için).\n" +
                        $"• 'Çekilmiş Dersler' + 'Ortak Dersler' + gün sayfaları da hazırlandı.\n" +
                        "• Beğenmezseniz Excel'i elle düzenleyip '3) Şablonu Yükle' ile geri yükleyin.";
                    if (tumUyarilar.Count > 0)
                        mesaj += $"\n\n⚠ {tumUyarilar.Count} uyarı var (ortak ders bölünmesi, sığmayan dersler vb.).";
                    mesaj += "\n\nDosya şimdi açılsın mı?";

                    if (MessageBox.Show(mesaj, "Otomatik Dağıtım Tamam",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dlg.FileName,
                                UseShellExecute = true
                            });
                        }
                        catch { /* açma başarısız olursa sessiz geç */ }
                    }

                    if (tumUyarilar.Count > 0)
                        AtlananSatirlariGoster(tumUyarilar);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Otomatik dağıtım hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Otomatik dağıtım başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  ADIM 3 — DOLDURULMUŞ ŞABLONU YÜKLE
        //  Kullanıcının elle doldurduğu "Sınav Haftası Dağıtım Şablonu" sayfasını okur;
        //  takvim (gün+saat) ve salonsuz yerleşim (şube atamaları) belleğe alınır.
        //  Salon ataması Adım 4'te uygulamanın kendi salon havuzuyla yapılır.
        // ─────────────────────────────────────────────────────────────
        private void BtnSablonYukle_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*";
                    dlg.Title = "Doldurulmuş Dağıtım Şablonunu Seçin";
                    dlg.FilterIndex = 1;

                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Doldurulmuş şablon okunuyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    var okuma = DagitimSablonuOkuyucu.Oku(dlg.FileName);

                    if (okuma.Sablon.Count == 0)
                    {
                        string uyariMesaji = "Şablondan hiç sınav okunamadı.\n" +
                            "('Sınav Haftası Dağıtım Şablonu' sayfasında gün/saat/ders yerleşimi beklenir.)";
                        if (okuma.Uyarilar.Count > 0)
                            uyariMesaji += $"\n\n{string.Join("\n", okuma.Uyarilar.Take(15))}";
                        MessageBox.Show(uyariMesaji, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblDurum.Text = "❌ Hiç sınav yüklenemedi.";
                        lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                        return;
                    }

                    int gunSayisi = okuma.Takvim.Count;
                    var bolumOzet = okuma.Sablon
                        .GroupBy(a => a.BolumAdi)
                        .Select(g => $"  • {g.Key}: {g.Select(x => x.DersAdi).Distinct().Count()} ders")
                        .OrderBy(s => s)
                        .ToList();

                    string ozet = $"Doldurulmuş şablon okundu:\n\n" +
                                  $"{gunSayisi} gün, {okuma.Sablon.Count} şube yerleşimi:\n\n{string.Join("\n", bolumOzet)}";
                    if (okuma.Uyarilar.Count > 0)
                        ozet += $"\n\n⚠ {okuma.Uyarilar.Count} satır için not/uyarı var (salon gerekmeyebilir).";

                    var onay = MessageBox.Show(
                        $"{ozet}\n\nBu yerleşim yüklensin mi?\n(Mevcut şablon temizlenecektir)",
                        "Şablon Yükleme Onayı",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (onay != DialogResult.Yes) return;

                    // ── Verileri belleğe yükle ──
                    _dersListesi.Clear();
                    _dersListesi.AddRange(okuma.Dersler);

                    // Şablondan gelen derslerde hoca bilgisi varsa eşlemeye ekle/kaydet
                    HocaEslemesiniGuncelleVeKaydet();

                    _takvimSlotlari.Clear();
                    foreach (var kvp in okuma.Takvim)
                        _takvimSlotlari[kvp.Key] = new List<string>(kvp.Value);

                    // Yüklenen takvimin gün sayısına göre sınav dönemi tipini senkronla
                    // (7+ gün → Final). Arayüzdeki seçim de buna göre güncellenir.
                    SinavTipiniTakvimdenSenkronla();

                    _sablonSonuclari = okuma.Sablon;
                    _sablonDuzenlendi = false;
                    _sablonIntNotlari.Clear();

                    DersGridiGuncelle();
                    UygulamaliDersGridiniGuncelle();
                    HataOraniGridiniGuncelle();

                    SablonGorunumuOlustur();

                    // Adım kilitleri: 4 açılır; 5 kapalı
                    btnSalonAta.Enabled = true;
                    btnGozetmenAta.Enabled = false;
                    btnMatrisExport.Enabled = false; btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;
                    _sonSonuclar = null;

                    tabControlSonuc.SelectedTab = tabSablon;

                    lblDurum.Text = $"✅ {gunSayisi} gün / {_sablonSonuclari.Count} şube yüklendi. " +
                                    "Hata payını girip '4) Salonları Ata'ya basın.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (okuma.Uyarilar.Count > 0)
                    {
                        var detayIste = MessageBox.Show(
                            $"{okuma.Uyarilar.Count} satır için not bulundu " +
                            "(ör. saat girilmemiş, kontenjan yok).\n\nDetayları görmek ister misiniz?",
                            "Yükleme Notları", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (detayIste == DialogResult.Yes)
                            AtlananSatirlariGoster(okuma.Uyarilar);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Şablon yükleme hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Şablon yüklemesi başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // Atlanan satırların detayını kaydırılabilir bir pencerede gösterir
        private void AtlananSatirlariGoster(List<string> atlananSatirlar)
        {
            var frm = new Form
            {
                Text = $"Atlanan Satırlar ({atlananSatirlar.Count} adet)",
                Size = new Size(700, 500),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = true
            };

            var txt = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                Text = string.Join(Environment.NewLine,
                    atlananSatirlar.Select((s, i) => $"{i + 1,4}. {s}"))
            };

            frm.Controls.Add(txt);
            frm.ShowDialog(this);
        }

        // =============================================
        // AŞAMALI SINAV PROGRAMI OLUŞTURMA
        // AŞAMA A: Şablon Oluştur (takvim yerleşimi, salonsuz)
        // AŞAMA B: Şablon üzerinde dinamik düzenleme (dgvSablon)
        // AŞAMA C: Salonları Ata (toptan salon dağıtımı)
        // AŞAMA D: Gözetmen Ata (mevcut modül, değişmedi)
        // =============================================

        /// <summary>Hata Oranı grid'inden bölüm bazlı % değerlerini okur.</summary>
        private Dictionary<string, double> HataOranlariniOku()
        {
            var bolumOverbooking = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgvHataOrani.Rows)
            {
                var bolum = row.Cells[0].Value?.ToString();
                if (string.IsNullOrWhiteSpace(bolum)) continue;
                string ham = row.Cells[1].Value?.ToString()?.Replace("%", "").Replace(',', '.').Trim();
                if (double.TryParse(ham, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out double oran)
                    && oran > 0 && oran <= 100)
                {
                    bolumOverbooking[bolum] = oran;
                }
            }
            return bolumOverbooking;
        }

        // NOT: "Şablon Oluştur" (Sınav Dağıtım Algoritması + Şablon/Saat üretimi) KALDIRILDI.
        // Şablon (gün/saat/ders yerleşimi) artık Excel'den (BAHAR FİNAL) hazır okunur —
        // bkz. BtnExcelImport_Click ve HazirSinavExcelOkuyucu.

        // ─────────────────────────────────────────────────────────────
        //  HAVUZ / ORTAK DERS YÖNETİMİ
        //  • Sınav listesi yüklendiği anda kullanılabilir (şablon şart değil).
        //  • Sekme 1 — ORTAK DERSLER: hangi derslerin ortak sayılacağını kullanıcı
        //    MANUEL işaretler; işaretlenenler aynı gün+saate çivilenir.
        //  • Sekme 2 — HAVUZ: bir ders için farklı bölümlerden seçilen şubeler
        //    TEK kontenjan gibi birleştirilip mümkünse aynı salona atanır.
        // ─────────────────────────────────────────────────────────────
        private void BtnHavuzYonetimi_Click(object sender, EventArgs e)
        {
            // Veri kaynağı: öncelik sınav listesi (_dersListesi); yoksa yüklü şablon
            var kaynak = new List<HavuzSubeSecim>();
            if (_dersListesi.Count > 0)
            {
                foreach (var d in _dersListesi)
                    kaynak.Add(new HavuzSubeSecim
                    {
                        DersAdi = (d.DersAdi ?? "").Trim(), DersKodu = (d.DersKodu ?? "").Trim(),
                        Bolum = d.BolumAdi, Sinif = d.SinifSeviyesi, Sube = d.Sube, Kontenjan = d.Kontenjan
                    });
            }
            else if (_sablonSonuclari != null && _sablonSonuclari.Count > 0)
            {
                foreach (var a in _sablonSonuclari)
                    kaynak.Add(new HavuzSubeSecim
                    {
                        DersAdi = (a.DersAdi ?? "").Trim(), DersKodu = "",
                        Bolum = a.BolumAdi, Sinif = a.SinifSeviyesi, Sube = a.Sube, Kontenjan = a.Kontenjan
                    });
            }

            if (kaynak.Count == 0)
            {
                MessageBox.Show("Önce sınav listesini (Excel) yükleyin!\n" +
                                "Ortak ders ve havuz üyeleri (bölüm/şube/kontenjan) bu listeden seçilir.",
                    "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var f = new Form
            {
                Text = "🔗 Havuz / Ortak Ders Yönetimi",
                Size = new Size(780, 620),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 46), ForeColor = Color.FromArgb(205, 214, 244)
            };

            var sekmeler = new TabControl { Location = new Point(10, 10), Size = new Size(748, 500) };
            var tabOrtak = new TabPage("⭐ Ortak Dersler (Manuel Seçim)") { BackColor = Color.FromArgb(30, 30, 46) };
            var tabHavuz = new TabPage("🔗 Havuz") { BackColor = Color.FromArgb(30, 30, 46) };
            sekmeler.TabPages.Add(tabOrtak);
            sekmeler.TabPages.Add(tabHavuz);

            // ══ SEKME 1 — ORTAK DERS SEÇİMİ ══
            var lblOrtakBilgi = new Label
            {
                Text = "Ortak sayılacak dersleri işaretleyin. İşaretlenen dersler tüm bölümlerinde AYNI gün ve saate çivilenir.\n" +
                       "Otomatik tespit kapalıdır — yalnızca burada işaretledikleriniz ortak kabul edilir.",
                Location = new Point(12, 10), Size = new Size(710, 34), ForeColor = Color.FromArgb(249, 226, 175)
            };
            var clbOrtak = new CheckedListBox
            {
                Location = new Point(12, 50), Size = new Size(710, 330), CheckOnClick = true,
                BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244)
            };
            var btnOner = new Button
            {
                Text = "✨ Önerilenleri İşaretle (≥2 bölümde okutulanlar)", Location = new Point(12, 392), Size = new Size(340, 34),
                BackColor = Color.FromArgb(137, 180, 250), ForeColor = Color.FromArgb(30, 30, 46), FlatStyle = FlatStyle.Flat
            };
            var btnOrtakTemizle = new Button
            {
                Text = "Temizle", Location = new Point(362, 392), Size = new Size(110, 34),
                BackColor = Color.FromArgb(88, 91, 112), ForeColor = Color.FromArgb(205, 214, 244), FlatStyle = FlatStyle.Flat
            };
            tabOrtak.Controls.AddRange(new Control[] { lblOrtakBilgi, clbOrtak, btnOner, btnOrtakTemizle });

            // Aday listesi: ders (kod + ad) bazında, hangi bölümlerde geçtiğiyle
            var adaylar = kaynak
                .GroupBy(k => string.IsNullOrEmpty(k.DersKodu) ? k.DersAdi.ToUpperInvariant() : k.DersKodu.ToUpperInvariant())
                .Select(g => new OrtakDersAday
                {
                    DersKodu = g.First().DersKodu,
                    DersAdi = g.First().DersAdi,
                    Bolumler = g.Select(x => x.Bolum).Distinct().OrderBy(b => b).ToList(),
                    ToplamKontenjan = g.Sum(x => x.Kontenjan)
                })
                .OrderByDescending(a => a.Bolumler.Count).ThenBy(a => a.DersAdi)
                .ToList();

            foreach (var a in adaylar)
            {
                int idx = clbOrtak.Items.Add(a);
                bool secili = _ortakSecimYapildi
                    ? (_manuelOrtakKodlar.Contains(a.DersKodu) || _manuelOrtakDersAdlari.Contains(a.DersAdi))
                    : a.Bolumler.Count >= 2; // ilk açılışta öneri: birden çok bölümde okutulanlar
                clbOrtak.SetItemChecked(idx, secili);
            }

            btnOner.Click += (s2, e2) =>
            {
                for (int i = 0; i < clbOrtak.Items.Count; i++)
                    clbOrtak.SetItemChecked(i, ((OrtakDersAday)clbOrtak.Items[i]).Bolumler.Count >= 2);
            };
            btnOrtakTemizle.Click += (s2, e2) =>
            {
                for (int i = 0; i < clbOrtak.Items.Count; i++) clbOrtak.SetItemChecked(i, false);
            };

            // ══ SEKME 2 — HAVUZ ══
            var lblDers = new Label { Text = "Ders:", Location = new Point(12, 12), AutoSize = true };
            var cmbDers = new ComboBox
            {
                Location = new Point(12, 35), Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244)
            };
            var lblSubeler = new Label { Text = "Havuza alınacak şubeleri seçin (en az 2):", Location = new Point(12, 68), AutoSize = true };
            var clbSubeler = new CheckedListBox
            {
                Location = new Point(12, 91), Size = new Size(420, 290), CheckOnClick = true,
                BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244)
            };
            var lblHavuzlar = new Label { Text = "Tanımlı havuzlar:", Location = new Point(448, 68), AutoSize = true };
            var lstHavuzlar = new ListBox
            {
                Location = new Point(448, 91), Size = new Size(275, 290),
                BackColor = Color.FromArgb(49, 50, 68), ForeColor = Color.FromArgb(205, 214, 244)
            };
            var btnOlustur = new Button
            {
                Text = "➕ Havuz Oluştur", Location = new Point(12, 392), Size = new Size(180, 36),
                BackColor = Color.FromArgb(166, 227, 161), ForeColor = Color.FromArgb(30, 30, 46), FlatStyle = FlatStyle.Flat
            };
            var btnSil = new Button
            {
                Text = "🗑 Seçili Havuzu Sil", Location = new Point(448, 392), Size = new Size(180, 36),
                BackColor = Color.FromArgb(243, 139, 168), ForeColor = Color.FromArgb(30, 30, 46), FlatStyle = FlatStyle.Flat
            };
            tabHavuz.Controls.AddRange(new Control[] { lblDers, cmbDers, lblSubeler, clbSubeler, lblHavuzlar, lstHavuzlar, btnOlustur, btnSil });

            var btnKapat = new Button
            {
                Text = "✔ Kaydet ve Kapat", Location = new Point(590, 522), Size = new Size(168, 36),
                BackColor = Color.FromArgb(166, 227, 161), ForeColor = Color.FromArgb(30, 30, 46),
                FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK
            };

            void HavuzlariYenile()
            {
                lstHavuzlar.Items.Clear();
                foreach (var h in _sablonHavuzlari)
                {
                    string uyeler = string.Join(" + ", h.Uyeler.Select(u => $"{u.Bolum}({u.Sube})"));
                    lstHavuzlar.Items.Add($"{h.DersAdi} → {uyeler}");
                }
            }

            void SubeleriYenile()
            {
                clbSubeler.Items.Clear();
                if (cmbDers.SelectedItem == null) return;
                string ders = cmbDers.SelectedItem.ToString();
                foreach (var k in kaynak
                    .Where(k => string.Equals(k.DersAdi, ders, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(k => k.Bolum).ThenBy(k => k.Sube))
                    clbSubeler.Items.Add(k);
            }

            // Havuz ders listesi: ≥2 şubesi (bölüm×şube kombinasyonu) olan dersler
            var havuzDersleri = kaynak
                .GroupBy(k => k.DersAdi, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Select(x => $"{x.Bolum}|{x.Sube}").Distinct().Count() >= 2)
                .Select(g => g.Key).OrderBy(d => d).ToList();
            foreach (var d in havuzDersleri) cmbDers.Items.Add(d);

            cmbDers.SelectedIndexChanged += (s2, e2) => SubeleriYenile();
            if (cmbDers.Items.Count > 0) cmbDers.SelectedIndex = 0;
            HavuzlariYenile();

            btnOlustur.Click += (s2, e2) =>
            {
                var secili = clbSubeler.CheckedItems.Cast<HavuzSubeSecim>().ToList();
                if (cmbDers.SelectedItem == null || secili.Count < 2)
                {
                    MessageBox.Show("Havuz için en az 2 şube seçmelisiniz.", "Uyarı",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                var havuz = new DersHavuzu { DersAdi = cmbDers.SelectedItem.ToString() };
                foreach (var sec in secili)
                {
                    havuz.Uyeler.Add(new HavuzUyesi { Bolum = sec.Bolum, Sube = sec.Sube, SinifSeviyesi = sec.Sinif });
                    if (!havuz.Bolumler.Contains(sec.Bolum)) havuz.Bolumler.Add(sec.Bolum);
                }
                _sablonHavuzlari.Add(havuz);
                HavuzlariYenile();
                for (int i = 0; i < clbSubeler.Items.Count; i++) clbSubeler.SetItemChecked(i, false);
            };

            btnSil.Click += (s2, e2) =>
            {
                if (lstHavuzlar.SelectedIndex < 0) return;
                _sablonHavuzlari.RemoveAt(lstHavuzlar.SelectedIndex);
                HavuzlariYenile();
            };

            f.Controls.AddRange(new Control[] { sekmeler, btnKapat });
            f.AcceptButton = btnKapat;
            f.ShowDialog(this);

            // ── Ortak ders seçimini KAYDET (diyalog nasıl kapanırsa kapansın son durum geçerli) ──
            _manuelOrtakKodlar.Clear();
            _manuelOrtakDersAdlari.Clear();
            foreach (OrtakDersAday a in clbOrtak.CheckedItems)
            {
                if (!string.IsNullOrEmpty(a.DersKodu)) _manuelOrtakKodlar.Add(a.DersKodu);
                _manuelOrtakDersAdlari.Add(a.DersAdi);
            }
            _ortakSecimYapildi = true;

            lblDurum.Text = $"⭐ {_manuelOrtakDersAdlari.Count} ortak ders (manuel) • 🔗 {_sablonHavuzlari.Count} havuz tanımlı.";
            lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
        }

        /// <summary>Havuz/Ortak diyaloğunda listelenen şube satırı.</summary>
        private class HavuzSubeSecim
        {
            public string DersAdi; public string DersKodu;
            public string Bolum; public int Sinif; public string Sube; public int Kontenjan;
            public override string ToString() => $"{Bolum} — {Sinif}. Sınıf, Şube {Sube} ({Kontenjan} öğr.)";
        }

        // ═════════════════════════════════════════════════════════════
        //  HOCA EŞLEMESİ — kalıcı kayıt
        //  İlk yüklenen sınav listesi Excel'indeki Öğretim Elemanı bilgisi
        //  import anında bu sözlüğe alınır ve DOSYAYA KAYDEDİLİR; Sınav
        //  Programı Formu'nun hoca sütunu (ders listesi bellekte olmasa da)
        //  bu kayıttan doldurulur.
        // ═════════════════════════════════════════════════════════════

        /// <summary>"dersNorm|BÖLÜM|ŞUBE" ve "dersNorm|BÖLÜM" → hoca adı.</summary>
        private Dictionary<string, string> _hocaEslemesi =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string HocaEslemeDosyasi => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SinavProgrami", "hoca_eslemesi.json");

        private static string HocaNAd(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s.ToUpperInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static string HocaBolumKisalt(string b)
        {
            if (string.IsNullOrWhiteSpace(b)) return "?";
            string u = b.ToUpperInvariant();
            if (u.Contains("BİLG") || u.Contains("BILG")) return "BİL";
            if (u.Contains("ELEKTR") || u.Contains("EEM")) return "EEM";
            if (u.Contains("İNŞA") || u.Contains("INSA") || u.Contains("INS")) return "INS";
            if (u.Contains("MEKATRON") || u.Contains("MEK")) return "MEK";
            if (u.Contains("METALURJ") || u.Contains("MALZEME") || u.Contains("MEM")) return "MEM";
            if (u.Contains("MAKİN") || u.Contains("MAKIN") || u.Contains("MAK")) return "MAK";
            return u.Trim().Length >= 3 ? u.Trim().Substring(0, 3) : u.Trim();
        }

        /// <summary>Şube etiketini A/B/C/D harf kolonuna indirger (eski "INT" → "D").</summary>
        private static string HocaSubeHarf(string sube)
            => SubeEtiketi.Normalize(sube, SubeEtiketi.DordunculSube);

        /// <summary>
        /// Sınav listesi (Excel) yüklendiğinde çağrılır: hoca eşlemesini
        /// _dersListesi'nden çıkarır ve kalıcı dosyaya kaydeder.
        /// </summary>
        private void HocaEslemesiniGuncelleVeKaydet()
        {
            try
            {
                foreach (var d in _dersListesi)
                {
                    if (string.IsNullOrWhiteSpace(d.OgretimElemani)) continue;
                    string hoca = d.OgretimElemani.Trim();
                    string dersNorm = HocaNAd(d.DersAdi);
                    string kok = $"{dersNorm}|{HocaBolumKisalt(d.BolumAdi)}";
                    _hocaEslemesi[$"{kok}|{HocaSubeHarf(d.Sube)}"] = hoca; // şube bazlı (üzerine yazar)
                    if (!_hocaEslemesi.ContainsKey(kok)) _hocaEslemesi[kok] = hoca;        // ders + bölüm
                    if (!_hocaEslemesi.ContainsKey(dersNorm)) _hocaEslemesi[dersNorm] = hoca; // yalnız ders (son çare)
                }

                Directory.CreateDirectory(Path.GetDirectoryName(HocaEslemeDosyasi));
                File.WriteAllText(HocaEslemeDosyasi,
                    JsonSerializer.Serialize(_hocaEslemesi,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hoca eşlemesi kaydedilemedi: {ex.Message}");
            }
        }

        /// <summary>Kaydedilmiş hoca eşlemesini uygulama açılışında dosyadan yükler.</summary>
        private void HocaEslemesiniYukle()
        {
            try
            {
                if (!File.Exists(HocaEslemeDosyasi)) return;
                var okunan = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(HocaEslemeDosyasi));
                if (okunan != null)
                    _hocaEslemesi = new Dictionary<string, string>(okunan, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hoca eşlemesi okunamadı: {ex.Message}");
            }
        }

        /// <summary>
        /// Şablon satırlarındaki BOŞ Öğretim Elemanı alanlarını KALICI hoca
        /// eşlemesinden doldurur (kaynak: ilk yüklenen sınav listesi Excel'i).
        /// Eşleşme: normalize ders adı + bölüm kısaltması (+ şube); şube tutmazsa
        /// dersin o bölümdeki genel hocası kullanılır.
        /// </summary>
        private void OgretimElemanlariniDoldur(List<SinavAtama> sablon)
        {
            if (sablon == null || sablon.Count == 0) return;

            // Bellekteki ders listesi güncelse eşlemeyi tazele (dosyaya da yazılır)
            if (_dersListesi.Count > 0) HocaEslemesiniGuncelleVeKaydet();
            if (_hocaEslemesi.Count == 0) return;

            foreach (var a in sablon)
            {
                if (!string.IsNullOrWhiteSpace(a.OgretimElemani)) continue;
                string dersNorm = HocaNAd(a.DersAdi);
                string kok = $"{dersNorm}|{HocaBolumKisalt(a.BolumAdi)}";
                // Sıra: ders+bölüm+şube → ders+bölüm → yalnız ders (bölüm formatı tutmazsa)
                if (_hocaEslemesi.TryGetValue($"{kok}|{HocaSubeHarf(a.Sube)}", out string hoca) ||
                    _hocaEslemesi.TryGetValue(kok, out hoca) ||
                    _hocaEslemesi.TryGetValue(dersNorm, out hoca))
                    a.OgretimElemani = hoca;
            }
        }

        /// <summary>
        /// Ders kodu bölüm öneklerinden (BIL, ELM, INS, MAK, MEK, MEM) biriyle
        /// BAŞLAMIYORSA ders "ortak/esnetilebilir" kabul edilir (örn. ATA101, TUR102, FIZ...).
        /// </summary>
        private static bool KodBolumDisiMi(string dersKodu)
        {
            if (string.IsNullOrWhiteSpace(dersKodu)) return false;
            var sb = new StringBuilder();
            foreach (char c in dersKodu.Trim())
            {
                if (char.IsDigit(c)) break;
                if (char.IsLetter(c)) sb.Append(char.ToUpperInvariant(c == 'i' ? 'I' : c));
            }
            string onek = sb.ToString().Replace("İ", "I");
            return onek.Length > 0 &&
                   onek != "BIL" && onek != "ELM" && onek != "INS" &&
                   onek != "MAK" && onek != "MEK" && onek != "MEM" && onek != "EEM";
        }

        /// <summary>Ortak ders sekmesindeki aday satırı.</summary>
        private class OrtakDersAday
        {
            public string DersKodu; public string DersAdi;
            public List<string> Bolumler; public int ToplamKontenjan;
            public override string ToString()
            {
                string kod = string.IsNullOrEmpty(DersKodu) ? "" : $"[{DersKodu}] ";
                return $"{kod}{DersAdi} — {string.Join("+", Bolumler)} ({ToplamKontenjan} öğr.)";
            }
        }

        /// <summary>
        /// SALONLARI ATA: Excel'den okunan (ve gerekiyorsa elle düzenlenen) şablon girdi alınarak
        /// SADECE salon dağıtımı yapılır. Hata payı (net kontenjan), blok sabitleme, artık eşiği,
        /// uygulamalı/normal salon ayrımı aynen korunur.
        /// </summary>
        private void BtnSalonAta_Click(object sender, EventArgs e)
        {
            try
            {
                if (_sablonSonuclari == null)
                {
                    MessageBox.Show("Önce '3) Şablonu Yükle' ile doldurulmuş dağıtım şablonunu yükleyin!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_sablonSonuclari.Count == 0)
                {
                    MessageBox.Show("Şablon boş!\nSalon ataması için şablona en az bir ders yerleştirin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lblDurum.Text = "⏳ Salonlar çekiliyor..."; lblDurum.ForeColor = Color.FromArgb(249, 226, 175); Application.DoEvents();
                List<Salon> salonlar;
                try { salonlar = DatabaseHelper.SalonlariGetir(); _cachedSalonlar = salonlar; }
                catch (Exception dbEx) { MessageBox.Show($"Salon bilgileri alınamadı:\n{dbEx.Message}", "DB Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                if (salonlar.Count == 0) { MessageBox.Show("Veritabanında salon bulunamadı!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                lblDurum.Text = $"⏳ Salon dağıtımı yapılıyor (ortak önce, ELM+MEK → T4)..."; Application.DoEvents();

                // ── Günlük salon dağıtımı: ortak önce, ELM+MEK T4 önceliği, uygulamalı ayrı, kapasiteye göre ──
                // (SADECE atama yapılır; Excel çıktısı 'Salon Atanmış Dosya Çıkart' ile alınır.)
                // Şablon Excel'inde hoca kolonu yoktur — Öğretim Elemanı bilgisi
                // sınav listesinden (ders+bölüm+şube eşleşmesiyle) tamamlanır.
                OgretimElemanlariniDoldur(_sablonSonuclari);

                var dagitici = new GunlukSalonDagitici();
                // Ortak dersler: manuel seçim varsa SADECE o liste; yoksa otomatik (aynı ad ≥2 bölüm)
                var gunSatirlari = dagitici.Dagit(_sablonSonuclari, salonlar, _uygulamaliDersler, _sablonHavuzlari,
                                                  _ortakSecimYapildi ? _manuelOrtakDersAdlari : null);
                if (gunSatirlari.Count == 0)
                {
                    MessageBox.Show("Şablondan salon dağıtımı üretilemedi.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _gunSalonSatirlari = gunSatirlari;
                _sonSonuclar = GunlukSalonDagitici.SinavAtamalarinaCevir(gunSatirlari, salonlar);
                try { SonuclariGoster(_sonSonuclar); MatrisGorunumuOlustur(_sonSonuclar, salonlar); } catch { /* görünüm opsiyonel */ }

                // Buton durumu: salon atandı → 'Salon Atanmış Dosya Çıkart' aktif
                btnSalonExport.Enabled = true;
                // Yeni salon dağılımı → önceki gözetmenli taslak/düzenleme geçersiz
                btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                int gunSayisi = gunSatirlari.Select(s => s.Gun).Distinct().Count();
                if (dagitici.Uyarilar.Count > 0)
                {
                    lblDurum.Text = $"⚠ {gunSayisi} gün / {gunSatirlari.Count} ders salona atandı, {dagitici.Uyarilar.Count} uyarı! 'Salon Atanmış Dosya Çıkart'a basın.";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    AtlananSatirlariGoster(dagitici.Uyarilar);
                }
                else
                {
                    lblDurum.Text = $"✅ Salon ataması tamam: {gunSayisi} gün, {gunSatirlari.Count} ders. Şimdi 'Salon Atanmış Dosya Çıkart'a basın.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Salon atama hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Salon ataması başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  SALON ATANMIŞ DOSYA ÇIKART — gün sayfaları salon matrisi Excel'i
        // ─────────────────────────────────────────────────────────────
        private void BtnSalonExport_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gunSalonSatirlari == null || _gunSalonSatirlari.Count == 0)
                {
                    MessageBox.Show("Önce 'Salonları Ata' ile salon dağıtımını yapın!",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx";
                    dlg.Title = "Salon Atanmış Dosyayı Kaydet";
                    dlg.FileName = $"Salon_Atanmis_{DateTime.Now:yyyyMMdd}.xlsx";
                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Salon atanmış dosya oluşturuluyor..."; lblDurum.ForeColor = Color.FromArgb(249, 226, 175); Application.DoEvents();

                    GunSalonMatrisExportHelper.YazVeKaydet(_gunSalonSatirlari, _cachedSalonlar, _sablonSonuclari, _takvimSlotlari, dlg.FileName);

                    lblDurum.Text = "✅ Salon atanmış dosya oluşturuldu. Gerekirse elle düzenleyip 'Gözetmenler için Dosya Yükle' ile verin.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (MessageBox.Show("Salon atanmış dosya oluşturuldu:\n\n" + dlg.FileName +
                        "\n\nGün sayfalarında salonlar atanmış (matris) + Sınav Haftası Dağıtım Şablonu sayfası içerir.\n\nDosya açılsın mı?",
                        "Dosya Hazır", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dlg.FileName, UseShellExecute = true }); }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya çıkartma hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Dosya çıkartılamadı!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  GÖZETMENLER İÇİN DOSYA YÜKLE — salon atanmış dosyayı geri oku
        //  (kullanıcı salonları elle düzenlemiş olabilir); gözetmen ataması buna göre yapılır.
        // ─────────────────────────────────────────────────────────────
        private void BtnGozetmenDosyaYukle_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*";
                    dlg.Title = "Salon Atanmış Dosyayı Seçin (gözetmen ataması için)";
                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Salon atanmış dosya okunuyor..."; lblDurum.ForeColor = Color.FromArgb(249, 226, 175); Application.DoEvents();

                    var okuma = GunSalonMatrisOkuyucu.Oku(dlg.FileName);
                    if (okuma.Atamalar.Count == 0)
                    {
                        string m = "Dosyadan salon atanmış sınav okunamadı.\n('Sınav Günü' matris sayfaları beklenir.)";
                        if (okuma.Uyarilar.Count > 0) m += "\n\n" + string.Join("\n", okuma.Uyarilar.Take(12));
                        MessageBox.Show(m, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblDurum.Text = "❌ Salon atanmış dosya okunamadı."; lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                        return;
                    }

                    // Salonları da tazele (kapasite/tip için)
                    try { _cachedSalonlar = DatabaseHelper.SalonlariGetir(); } catch { }

                    _sonSonuclar = okuma.Atamalar;
                    // Bu dosyada hoca kolonu yok — Öğretim Elemanı kalıcı eşlemeden tamamlanır
                    OgretimElemanlariniDoldur(_sonSonuclar);
                    try { SonuclariGoster(_sonSonuclar); MatrisGorunumuOlustur(_sonSonuclar, _cachedSalonlar); } catch { }

                    btnGozetmenAta.Enabled = true;
                    btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                    int gun = okuma.Atamalar.Select(a => a.Gun).Distinct().Count();
                    lblDurum.Text = $"✅ {gun} gün / {okuma.Atamalar.Count} ders (salon atanmış) yüklendi. 'Gözetmen Ata'ya basın.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (okuma.Uyarilar.Count > 0) AtlananSatirlariGoster(okuma.Uyarilar);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya okuma hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Dosya okunamadı!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // =============================================
        // ŞABLON GÖRÜNÜMÜ (dgvSablon) — AŞAMA A/B
        // Fakültenin resmi final programı formatı taklit edilir:
        // Sütunlar gün bazında gruplanır (Bölüm|Sınıf|Ders Adı|A|B|C|D),
        // satırlar saat bazında gruplanır (her saat bloğunda bölüm başına bir satır).
        // Salon bilgisi İÇERMEZ — salonlar Aşama C'de matris görünümünde gösterilir.
        // =============================================

        /// <summary>Türkçe ay adları (gün başlık bandı için).</summary>
        private static readonly string[] TurkceAylar =
            { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };

        /// <summary>Türkçe tam gün adları (gün başlık bandı için).</summary>
        private static readonly Dictionary<DayOfWeek, string> TurkceGunler = new Dictionary<DayOfWeek, string>
        {
            { DayOfWeek.Monday, "Pazartesi" }, { DayOfWeek.Tuesday, "Salı" }, { DayOfWeek.Wednesday, "Çarşamba" },
            { DayOfWeek.Thursday, "Perşembe" }, { DayOfWeek.Friday, "Cuma" }, { DayOfWeek.Saturday, "Cumartesi" }, { DayOfWeek.Sunday, "Pazar" }
        };

        /// <summary>
        /// Bölüm adını fakülte formatındaki kısaltmaya çevirir (BİL, EEM, İNŞ, MAK, MEK, MEM).
        /// Tanımsız bölümler için adın ilk 3 harfi kullanılır.
        /// </summary>
        private static string BolumKisalt(string bolumAdi)
        {
            if (string.IsNullOrWhiteSpace(bolumAdi)) return "?";
            string b = bolumAdi.ToUpperInvariant();
            if (b.Contains("BİLGİSAYAR") || b.Contains("BILGISAYAR")) return "BİL";
            if (b.Contains("ELEKTR")) return "EEM";
            if (b.Contains("İNŞAAT") || b.Contains("INSAAT")) return "İNŞ";
            if (b.Contains("MAKİNE") || b.Contains("MAKINE") || b.Contains("MAKİNA") || b.Contains("MAKINA")) return "MAK";
            if (b.Contains("MEKATRON")) return "MEK";
            if (b.Contains("METALURJ")) return "MEM";
            return b.Trim().Length >= 3 ? b.Trim().Substring(0, 3) : b.Trim();
        }

        /// <summary>Ders ComboBox öğesi formatı: "DERS ADI | 3. Sınıf".</summary>
        private static string DersItemYap(string dersAdi, int sinif) => $"{dersAdi} | {sinif}. Sınıf";

        /// <summary>DersItemYap formatını (dersAdi, sinif) olarak geri çözer.</summary>
        private static bool DersItemCoz(string item, out string dersAdi, out int sinif)
        {
            dersAdi = null; sinif = 0;
            if (string.IsNullOrWhiteSpace(item)) return false;
            int idx = item.LastIndexOf(" | ");
            if (idx <= 0) return false;
            dersAdi = item.Substring(0, idx).Trim();
            string son = item.Substring(idx + 3).Replace(". Sınıf", "").Trim();
            return int.TryParse(son, out sinif) && dersAdi.Length > 0;
        }

        /// <summary>INT notları için satır+gün anahtarı üretir.</summary>
        private static string IntNotAnahtari(int gunIdx, SablonSatirBilgi bilgi)
            => $"{gunIdx}|{bilgi.Saat}|{bilgi.BolumKisa}|{bilgi.GrupSira}";

        /// <summary>Şablon doğrulayıcısını güncel takvim/salon/uygulamalı/hata oranı bilgileriyle kurar.</summary>
        private void SablonDogrulayiciKur()
        {
            _sablonDogrulayici = new SablonDogrulayici(
                _takvimSlotlari, _cachedSalonlar, _uygulamaliDersler, HataOranlariniOku(), 1);
        }

        /// <summary>
        /// Şablon grid'ini _sablonSonuclari üzerinden yeniden kurar.
        /// Boş şablonda iskelet (günler × saatler × bölümler) açılır.
        /// </summary>
        private void SablonGorunumuOlustur()
        {
            _isUpdating = true;
            try
            {
                // Aktif hücre düzenlemesini iptal et — Rows.Clear() sırasında reentrancy önlenir
                dgvSablon.CancelEdit();
                if (dgvSablon.IsCurrentCellInEditMode)
                    dgvSablon.EndEdit();
                dgvSablon.CurrentCell = null;

                dgvSablon.SuspendLayout();
                dgvSablon.Columns.Clear();
                dgvSablon.Rows.Clear();

                // ÖNEMLİ: Satır şablonu BAZ yüksekliğe sıfırlanır. Zoom uygulanmışken grid
                // yeniden kurulursa zoomlu yükseklik "baz" olarak kaydediliyor ve her
                // düzenlemede satırlar kümülatif büyüyor/küçülüyordu (tablo bozulması).
                dgvSablon.RowTemplate.Height = 26;
                dgvSablon.ColumnHeadersHeight = 48;

                if (_sablonSonuclari == null) return;

                // ── Eksenler: günler, saatler, bölümler (takvim + ders listesinden dinamik) ──
                _sablonGunler = _takvimSlotlari.Keys.OrderBy(d => d).ToList();
                _sablonSaatler = _takvimSlotlari.Values.SelectMany(s => s).Distinct().OrderBy(s => s).ToList();

                var bolumAdlari = _dersListesi.Select(d => d.BolumAdi)
                    .Concat(_sablonSonuclari.Select(a => a.BolumAdi))
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (bolumAdlari.Count == 0) // hiç ders yoksa sabit bölüm listesi iskeleti
                    bolumAdlari = clbBolum.Items.Cast<object>().Select(o => o.ToString()).ToList();

                _sablonBolumler = bolumAdlari
                    .Select(b => (Kisa: BolumKisalt(b), Tam: b))
                    .OrderBy(x => x.Kisa)
                    .ToList();

                // ── Sabit Saat sütunu ──
                dgvSablon.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "Saat", HeaderText = "Saat", Width = 54, ReadOnly = true, Frozen = true
                });

                // ── Gün bazlı sütun blokları: Bölüm | Sınıf | Ders Adı | A | B | C | D ──
                for (int g = 0; g < _sablonGunler.Count; g++)
                {
                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_Bolum", HeaderText = "Bölüm", Width = 48, ReadOnly = true });
                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_Sinif", HeaderText = "Sınıf", Width = 40, ReadOnly = true });

                    var dersKolon = new DataGridViewComboBoxColumn
                    {
                        Name = $"g{g}_Ders",
                        HeaderText = "Ders Adı",
                        Width = 185,
                        FlatStyle = FlatStyle.Flat,
                        DisplayStyleForCurrentCellOnly = true
                    };
                    dersKolon.DefaultCellStyle.BackColor = Color.FromArgb(60, 62, 85);
                    dgvSablon.Columns.Add(dersKolon);

                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_A", HeaderText = "A", Width = 52 });
                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_B", HeaderText = "B", Width = 52 });
                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_C", HeaderText = "C", Width = 52 });
                    dgvSablon.Columns.Add(new DataGridViewTextBoxColumn { Name = $"g{g}_D", HeaderText = "D", Width = 48 });
                }

                // ── Hücre grupları: (günIdx, saat, bölüm) → ders grupları (ders+sınıf; şubeler birlikte) ──
                var hucreGruplari = new Dictionary<string, List<List<SinavAtama>>>();
                for (int g = 0; g < _sablonGunler.Count; g++)
                {
                    var gun = _sablonGunler[g].Date;
                    foreach (var atamaGrubu in _sablonSonuclari
                        .Where(a => a.Gun.Date == gun)
                        .GroupBy(a => new { a.Saat, Bolum = a.BolumAdi.ToUpperInvariant().Trim() }))
                    {
                        var dersGruplari = atamaGrubu
                            .GroupBy(a => new { Ders = a.DersAdi.ToUpperInvariant().Trim(), a.SinifSeviyesi })
                            .OrderBy(x => x.Key.SinifSeviyesi).ThenBy(x => x.Key.Ders)
                            .Select(x => x.OrderBy(a => a.Sube).ToList())
                            .ToList();
                        hucreGruplari[$"{g}|{atamaGrubu.Key.Saat}|{atamaGrubu.Key.Bolum}"] = dersGruplari;
                    }
                }

                // Bölüm başına ders ComboBox öğeleri (performans için önbellekli)
                var dersItemCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                List<string> DersItemleri(string bolumTam)
                {
                    if (dersItemCache.TryGetValue(bolumTam, out var mevcut)) return mevcut;
                    var itemler = new List<string> { "" }; // boş seçim = dersi şablondan çıkar
                    itemler.AddRange(_dersListesi
                        .Where(d => d.Kontenjan > 0 && string.Equals(d.BolumAdi?.Trim(), bolumTam, StringComparison.OrdinalIgnoreCase))
                        .Select(d => new { d.DersAdi, d.SinifSeviyesi })
                        .Distinct()
                        .OrderBy(x => x.SinifSeviyesi).ThenBy(x => x.DersAdi)
                        .Select(x => DersItemYap(x.DersAdi, x.SinifSeviyesi)));
                    dersItemCache[bolumTam] = itemler;
                    return itemler;
                }

                // ── Satırlar: saat blokları × bölümler (+ aynı hücrede birden çok ders varsa ek satır) ──
                int saatBlokIdx = 0;
                foreach (var saat in _sablonSaatler)
                {
                    bool saatinIlkSatiri = true;
                    Color blokRenk = saatBlokIdx % 2 == 0 ? Color.FromArgb(49, 50, 68) : Color.FromArgb(55, 56, 75);

                    foreach (var (kisa, tam) in _sablonBolumler)
                    {
                        string bolumUpper = tam.ToUpperInvariant().Trim();

                        // Bu (saat, bölüm) için gün başına grup sayısının maksimumu kadar satır gerekir
                        int satirSayisi = 1;
                        for (int g = 0; g < _sablonGunler.Count; g++)
                            if (hucreGruplari.TryGetValue($"{g}|{saat}|{bolumUpper}", out var gr) && gr.Count > satirSayisi)
                                satirSayisi = gr.Count;

                        for (int k = 0; k < satirSayisi; k++)
                        {
                            int rowIdx = dgvSablon.Rows.Add();
                            var row = dgvSablon.Rows[rowIdx];
                            row.DefaultCellStyle.BackColor = blokRenk;

                            var bilgi = new SablonSatirBilgi { Saat = saat, BolumKisa = kisa, BolumTam = tam, GrupSira = k };
                            row.Tag = bilgi;

                            // Saat hücresi sadece bloğun ilk satırında yazılır (birleşik görünüm)
                            var saatCell = row.Cells["Saat"];
                            saatCell.Value = saatinIlkSatiri ? saat : "";
                            saatCell.Style.BackColor = Color.FromArgb(69, 71, 90);
                            saatCell.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                            saatCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                            saatinIlkSatiri = false;

                            for (int g = 0; g < _sablonGunler.Count; g++)
                            {
                                var gun = _sablonGunler[g];
                                bool gunSaatVar = _takvimSlotlari.TryGetValue(gun, out var gunSaatleri) && gunSaatleri.Contains(saat);

                                var bolumCell = row.Cells[$"g{g}_Bolum"];
                                var sinifCell = row.Cells[$"g{g}_Sinif"];
                                var dersCell = (DataGridViewComboBoxCell)row.Cells[$"g{g}_Ders"];
                                var aCell = row.Cells[$"g{g}_A"];
                                var bCell = row.Cells[$"g{g}_B"];
                                var cCell = row.Cells[$"g{g}_C"];
                                var intCell = row.Cells[$"g{g}_D"];

                                bolumCell.Value = kisa;
                                bolumCell.ToolTipText = tam;
                                bolumCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                sinifCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                aCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                bCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                                cCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                                if (!gunSaatVar)
                                {
                                    // Bu günde bu saat slotu takvimde seçili değil → hücreler kapalı
                                    foreach (var c in new[] { bolumCell, sinifCell, (DataGridViewCell)dersCell, aCell, bCell, cCell, intCell })
                                    {
                                        c.ReadOnly = true;
                                        c.Style.BackColor = Color.FromArgb(40, 40, 55);
                                        c.Style.ForeColor = Color.FromArgb(108, 112, 134);
                                    }
                                    bilgi.GunGruplari[g] = null;
                                    continue;
                                }

                                // Ders ComboBox öğeleri (bu bölümün dersleri)
                                var itemler = DersItemleri(tam);
                                foreach (var it in itemler) dersCell.Items.Add(it);

                                List<SinavAtama> grup = null;
                                if (hucreGruplari.TryGetValue($"{g}|{saat}|{bolumUpper}", out var gruplar) && k < gruplar.Count)
                                    grup = gruplar[k];
                                bilgi.GunGruplari[g] = grup;

                                if (grup != null)
                                {
                                    var ilk = grup[0];
                                    string item = DersItemYap(ilk.DersAdi, ilk.SinifSeviyesi);
                                    if (!dersCell.Items.Contains(item)) dersCell.Items.Add(item);
                                    dersCell.Value = item;
                                    dersCell.ToolTipText = $"{ilk.DersAdi} — {grup.Count} şube";
                                    sinifCell.Value = ilk.SinifSeviyesi;

                                    // A / B / C sütunları: sıralı şube kontenjanları
                                    var subeHucreleri = new[] { aCell, bCell, cCell };
                                    for (int s = 0; s < grup.Count && s < 3; s++)
                                    {
                                        subeHucreleri[s].Value = grup[s].Kontenjan;
                                        subeHucreleri[s].ToolTipText = $"Şube {grup[s].Sube} — kontenjan (düzenlenebilir)";
                                    }
                                    if (grup.Count > 3)
                                        cCell.ToolTipText += $" | ⚠ +{grup.Count - 3} şube daha var (C sütununa sığmadı)";
                                }
                                else
                                {
                                    dersCell.Value = "";
                                    dersCell.ToolTipText = "Boş hücre — listeden ders seçerek yerleştirin";
                                }

                                // INT: serbest not alanı (kaydedilen not geri yüklenir)
                                if (_sablonIntNotlari.TryGetValue(IntNotAnahtari(g, bilgi), out string not))
                                    intCell.Value = not;
                                intCell.ToolTipText = "Serbest not alanı (INT)";
                            }
                        }
                    }
                    saatBlokIdx++;
                }

                dgvSablon.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                dgvSablon.ScrollBars = ScrollBars.Both;

                // Zoom için baz (referans) genişlik ve yükseklikleri kaydet
                _sablonBazSutunGenislikleri.Clear();
                foreach (DataGridViewColumn col in dgvSablon.Columns)
                    _sablonBazSutunGenislikleri[col.Name] = col.Width;
                _sablonBazSatirYuksekligi = dgvSablon.RowTemplate.Height;
            }
            finally
            {
                dgvSablon.ResumeLayout();
                _isUpdating = false;
            }

            // Kullanıcı daha önce zoom yaptıysa aynı zoom seviyesini yeni tabloya da uygula
            if (Math.Abs(_sablonFontBoyutu - SablonBazFontBoyutu) > 0.01F)
                SablonZoomUygula();

            dgvSablon.Invalidate(); // gün başlık bantları yeniden çizilsin
        }

        /// <summary>
        /// Sütun başlığı hücrelerini alt bölgeye çizer — üst bant, Paint olayında
        /// çizilen birleşik gün başlıklarına ayrılır.
        /// </summary>
        private void DgvSablon_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

            using (var arkaFirca = new SolidBrush(Color.FromArgb(69, 71, 90)))
            using (var cizgiKalem = new Pen(Color.FromArgb(49, 50, 68)))
            using (var baslikFont = new Font("Segoe UI", SablonBaslikFontBoyutu, FontStyle.Bold))
            {
                e.Graphics.FillRectangle(arkaFirca, e.CellBounds);
                e.Graphics.DrawLine(cizgiKalem, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom);

                var col = dgvSablon.Columns[e.ColumnIndex];
                bool gunSutunu = col.Name.StartsWith("g");
                var metinAlani = gunSutunu
                    ? new Rectangle(e.CellBounds.X, e.CellBounds.Y + SablonGunBandiYuksekligi, e.CellBounds.Width, e.CellBounds.Height - SablonGunBandiYuksekligi)
                    : e.CellBounds;

                TextRenderer.DrawText(e.Graphics, col.HeaderText, baslikFont, metinAlani,
                    Color.FromArgb(205, 214, 244),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            e.Handled = true;
        }

        /// <summary>
        /// Başlık satırının üst bandına birleşik gün başlıklarını çizer
        /// ("15 Haziran 2026 Pazartesi" gibi — 7 sütunluk gün bloğunu kaplar).
        /// </summary>
        private void DgvSablon_Paint(object sender, PaintEventArgs e)
        {
            if (dgvSablon.Columns.Count == 0 || _sablonGunler == null) return;

            using var bantFirca = new SolidBrush(Color.FromArgb(137, 180, 250));
            using var bantFont = new Font("Segoe UI", SablonBaslikFontBoyutu, FontStyle.Bold);
            using var ayracKalem = new Pen(Color.FromArgb(30, 30, 46), 2);

            // Donmuş "Saat" sütununun sağ kenarı — gün bantları bu sınırın soluna taşamaz
            // (yatay kaydırmada bandın donmuş sütun üzerine çizilip tabloyu bozmasını önler)
            int donmusSag = 0;
            var saatKolonu = dgvSablon.Columns["Saat"];
            if (saatKolonu != null)
            {
                var saatRect = dgvSablon.GetCellDisplayRectangle(saatKolonu.Index, -1, true);
                if (saatRect.Width > 0) donmusSag = saatRect.Right;
            }

            for (int g = 0; g < _sablonGunler.Count; g++)
            {
                // Gün bloğunun görünür sütun dikdörtgenlerinin birleşimi
                Rectangle birlesim = Rectangle.Empty;
                foreach (var ek in new[] { "_Bolum", "_Sinif", "_Ders", "_A", "_B", "_C", "_D" })
                {
                    var col = dgvSablon.Columns[$"g{g}{ek}"];
                    if (col == null) continue;
                    var r = dgvSablon.GetCellDisplayRectangle(col.Index, -1, true);
                    if (r.Width <= 0 || r.Height <= 0) continue;
                    birlesim = birlesim.IsEmpty ? r : Rectangle.Union(birlesim, r);
                }
                if (birlesim.IsEmpty) continue;

                // Donmuş sütun sınırına göre soldan kırp
                if (birlesim.X < donmusSag)
                {
                    int fark = donmusSag - birlesim.X;
                    if (fark >= birlesim.Width) continue; // tamamen donmuş alanın altında — çizme
                    birlesim.X += fark;
                    birlesim.Width -= fark;
                }

                var bant = new Rectangle(birlesim.X, birlesim.Y, birlesim.Width, SablonGunBandiYuksekligi);
                e.Graphics.FillRectangle(bantFirca, bant);
                e.Graphics.DrawLine(ayracKalem, bant.Left, bant.Top, bant.Left, birlesim.Bottom);

                var gun = _sablonGunler[g];
                string baslik = $"{gun.Day} {TurkceAylar[gun.Month]} {gun.Year} {TurkceGunler[gun.DayOfWeek]}";
                TextRenderer.DrawText(e.Graphics, baslik, bantFont, bant,
                    Color.FromArgb(30, 30, 46),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        // ── AŞAMA B: ŞABLON ÜZERİNDE DİNAMİK DÜZENLEME ──

        /// <summary>
        /// Ders ComboBox'ı düzenlemeye girerken Excel rahatlığı sağlanır:
        /// serbest yazma + autocomplete (yazarken öneri) açılır, açılır listeden
        /// seçim yapılır yapılmaz değişiklik anında uygulanır.
        /// </summary>
        private void DgvSablon_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is ComboBox cb)
            {
                cb.DropDownStyle = ComboBoxStyle.DropDown;           // yazarak arama serbest
                cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend; // yazarken öneri listesi
                cb.AutoCompleteSource = AutoCompleteSource.ListItems;
                cb.Font = new Font("Segoe UI", _sablonFontBoyutu);

                // Aynı editing control tekrar kullanıldığından önce çöz, sonra bağla
                cb.SelectionChangeCommitted -= SablonComboSecimOnaylandi;
                cb.SelectionChangeCommitted += SablonComboSecimOnaylandi;
            }
        }

        /// <summary>Açılır listeden seçim yapılır yapılmaz değişikliği uygular (hücreden çıkmayı beklemez).</summary>
        private void SablonComboSecimOnaylandi(object sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                if (dgvSablon.IsCurrentCellDirty)
                    dgvSablon.CommitEdit(DataGridViewDataErrorContexts.Commit);
                dgvSablon.EndEdit();
            }));
        }

        /// <summary>Delete tuşu: seçili Ders hücrelerindeki dersleri şablondan çıkarır (Excel rahatlığı).</summary>
        private void DgvSablon_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete || _sablonSonuclari == null || dgvSablon.IsCurrentCellInEditMode) return;

            var cikarilacaklar = new List<SinavAtama>();
            foreach (DataGridViewCell cell in dgvSablon.SelectedCells)
            {
                var col = dgvSablon.Columns[cell.ColumnIndex];
                if (!col.Name.EndsWith("_Ders")) continue;

                int gunIdx = SablonGunIndeksi(col.Name);
                if (gunIdx < 0) continue;

                var row = dgvSablon.Rows[cell.RowIndex];
                if (row.Tag is SablonSatirBilgi bilgi &&
                    bilgi.GunGruplari.TryGetValue(gunIdx, out var grup) && grup != null)
                    cikarilacaklar.AddRange(grup);
            }

            if (cikarilacaklar.Count == 0) return;

            _sablonSonuclari.RemoveAll(a => cikarilacaklar.Contains(a));
            _sablonDuzenlendi = true;
            SablonYenile();
            e.Handled = true;

            lblDurum.Text = $"🗑 {cikarilacaklar.Count} şube yerleşimi şablondan çıkarıldı (Delete).";
            lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
        }

        /// <summary>
        /// Ctrl + fare tekerleği ile şablon tablosunda zoom in/out
        /// (matris görünümündeki davranışın aynısı, font 6–16 arası).
        /// </summary>
        private void DgvSablon_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control) return;

            // Ctrl basılıyken tablonun kaymasını engelle
            if (e is HandledMouseEventArgs he)
                he.Handled = true;

            float yeniBoyut = _sablonFontBoyutu + (e.Delta > 0 ? 1F : -1F);
            yeniBoyut = Math.Max(SablonMinFontBoyutu, Math.Min(SablonMaxFontBoyutu, yeniBoyut));
            if (Math.Abs(yeniBoyut - _sablonFontBoyutu) < 0.01F) return;

            _sablonFontBoyutu = yeniBoyut;
            SablonZoomUygula();
        }

        /// <summary>
        /// Mevcut zoom seviyesini şablon tablosuna uygular; sütun genişlikleri,
        /// satır yükseklikleri, başlık yüksekliği ve gün bandı orantılı ölçeklenir.
        /// </summary>
        private void SablonZoomUygula()
        {
            if (dgvSablon.Columns.Count == 0) return;

            float oran = _sablonFontBoyutu / SablonBazFontBoyutu;

            dgvSablon.SuspendLayout();
            try
            {
                dgvSablon.DefaultCellStyle.Font = new Font("Segoe UI", _sablonFontBoyutu);

                // Sütun genişliklerini baz genişlik üzerinden ölçekle (kümülatif yuvarlama hatası olmaz)
                foreach (DataGridViewColumn col in dgvSablon.Columns)
                {
                    if (_sablonBazSutunGenislikleri.TryGetValue(col.Name, out int bazGenislik))
                        col.Width = Math.Max(20, (int)Math.Round(bazGenislik * oran));
                }

                // Başlık yüksekliği: gün bandı + sütun adı bölgesi (baz: 48)
                dgvSablon.ColumnHeadersHeight = Math.Max(30, (int)Math.Round(48 * oran));

                // Satır yüksekliklerini ölçekle
                int bazYukseklik = _sablonBazSatirYuksekligi > 0 ? _sablonBazSatirYuksekligi : 26;
                int yeniYukseklik = Math.Max(14, (int)Math.Round(bazYukseklik * oran));
                dgvSablon.RowTemplate.Height = yeniYukseklik;

                foreach (DataGridViewRow row in dgvSablon.Rows)
                {
                    row.Height = yeniYukseklik;

                    // Özel fontlu hücreleri (örn. kalın saat hücreleri) yeni boyuta taşı
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.HasStyle && cell.Style.Font != null)
                            cell.Style.Font = new Font(cell.Style.Font.FontFamily, _sablonFontBoyutu, cell.Style.Font.Style);
                    }
                }
            }
            finally
            {
                dgvSablon.ResumeLayout();
            }

            dgvSablon.Invalidate(); // gün başlık bantları yeni ölçekle çizilsin
        }

        /// <summary>Ders ComboBox hücresi değiştiğinde: ders ekleme / çıkarma / gün-saat taşıma.</summary>
        private void DgvSablon_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = dgvSablon.Columns[e.ColumnIndex];
            if (!col.Name.EndsWith("_Ders")) return;

            int gunIdx = SablonGunIndeksi(col.Name);
            if (gunIdx < 0) return;

            var row = dgvSablon.Rows[e.RowIndex];
            string secim = row.Cells[e.ColumnIndex].Value?.ToString() ?? "";
            SablonDersSecimiUygula(row, gunIdx, secim);
        }

        /// <summary>
        /// ComboBox'a serbest yazılan metni bölümün ders listesindeki bir derse eşler
        /// (Excel rahatlığı — önce birebir, sonra "ile başlayan" tek eşleşme aranır).
        /// </summary>
        private bool YazilanMetniDerseEsle(string metin, string bolumTam, out string dersAdi, out int sinif)
        {
            dersAdi = null; sinif = 0;
            metin = metin?.Trim();
            if (string.IsNullOrEmpty(metin)) return false;

            var trCompare = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
            var adaylar = _dersListesi
                .Where(d => d.Kontenjan > 0 && d.BolumAdi.Equals(bolumTam, StringComparison.OrdinalIgnoreCase))
                .Select(d => new { d.DersAdi, d.SinifSeviyesi })
                .Distinct()
                .ToList();

            // 1) Birebir ders adı eşleşmesi (Türkçe, büyük/küçük harf bağımsız)
            var tam = adaylar.Where(a =>
                trCompare.Compare(a.DersAdi, metin, System.Globalization.CompareOptions.IgnoreCase) == 0).ToList();
            if (tam.Count == 1) { dersAdi = tam[0].DersAdi; sinif = tam[0].SinifSeviyesi; return true; }

            // 2) "İle başlayan" TEK eşleşme (kısmi yazımda güvenli tamamlama)
            var baslayan = adaylar.Where(a =>
                trCompare.IsPrefix(a.DersAdi, metin, System.Globalization.CompareOptions.IgnoreCase)).ToList();
            if (baslayan.Count == 1) { dersAdi = baslayan[0].DersAdi; sinif = baslayan[0].SinifSeviyesi; return true; }

            return false;
        }

        /// <summary>"g3_Ders" gibi sütun adından gün indeksini çözer.</summary>
        private static int SablonGunIndeksi(string kolonAdi)
        {
            if (string.IsNullOrEmpty(kolonAdi) || kolonAdi[0] != 'g') return -1;
            int alt = kolonAdi.IndexOf('_');
            if (alt <= 1) return -1;
            return int.TryParse(kolonAdi.Substring(1, alt - 1), out int idx) ? idx : -1;
        }

        /// <summary>
        /// Ders hücresi seçimini şablon modeline uygular:
        /// • Boş seçim → hücredeki ders şablondan çıkarılır.
        /// • Ders zaten şablonun başka slotundaysa → o slottan buraya TAŞINIR (gün/saat değişimi).
        /// • Değilse → _dersListesi'ndeki tüm şubeleriyle bu slota EKLENİR.
        /// Kural ihlalleri anında kontrol edilir ama ENGELLEMEZ — sadece görsel uyarı verilir.
        /// </summary>
        private void SablonDersSecimiUygula(DataGridViewRow row, int gunIdx, string secim)
        {
            if (!(row.Tag is SablonSatirBilgi bilgi) || _sablonSonuclari == null) return;
            if (gunIdx < 0 || gunIdx >= _sablonGunler.Count) return;

            var gun = _sablonGunler[gunIdx];
            bilgi.GunGruplari.TryGetValue(gunIdx, out var eskiGrup);

            if (string.IsNullOrWhiteSpace(secim))
            {
                // ── DERSİ ŞABLONDAN ÇIKAR ──
                if (eskiGrup == null || eskiGrup.Count == 0) return;
                string cikarilan = eskiGrup[0].DersAdi;
                _sablonSonuclari.RemoveAll(a => eskiGrup.Contains(a));
                _sablonDuzenlendi = true;
                SablonYenile();
                lblDurum.Text = $"🗑 '{cikarilan}' şablondan çıkarıldı.";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                return;
            }

            // Önce standart öğe formatını çöz; olmuyorsa serbest yazılan metni derse eşle
            if (!DersItemCoz(secim, out string dersAdi, out int sinif) &&
                !YazilanMetniDerseEsle(secim, bilgi.BolumTam, out dersAdi, out sinif))
            {
                lblDurum.Text = $"⚠ '{secim}' bu bölümün ders listesinde bulunamadı — listeden seçin veya tam adı yazın.";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                SablonYenile(); // hücreyi eski değerine döndür
                return;
            }

            // Aynı ders zaten bu hücredeyse işlem yok
            if (eskiGrup != null && eskiGrup.Count > 0 &&
                eskiGrup[0].DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase) &&
                eskiGrup[0].SinifSeviyesi == sinif)
                return;

            // Hücrede başka ders varsa önce onu çıkar
            if (eskiGrup != null && eskiGrup.Count > 0)
                _sablonSonuclari.RemoveAll(a => eskiGrup.Contains(a));

            // Ders şablonda başka slotta var mı? → TAŞI (gün/saat değişimi)
            var mevcutAtamalar = _sablonSonuclari
                .Where(a => a.BolumAdi.Equals(bilgi.BolumTam, StringComparison.OrdinalIgnoreCase)
                         && a.DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase)
                         && a.SinifSeviyesi == sinif)
                .ToList();

            if (mevcutAtamalar.Count > 0)
            {
                foreach (var a in mevcutAtamalar) { a.Gun = gun; a.Saat = bilgi.Saat; }
                lblDurum.Text = $"📌 '{dersAdi}' → {gun:dd.MM.yyyy} {bilgi.Saat} slotuna taşındı ({mevcutAtamalar.Count} şube).";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
            }
            else
            {
                // ── YENİ EKLEME: dersin tüm şubeleri bu slota yerleştirilir ──
                var subeler = _dersListesi
                    .Where(d => d.Kontenjan > 0
                             && d.BolumAdi.Equals(bilgi.BolumTam, StringComparison.OrdinalIgnoreCase)
                             && d.DersAdi.Equals(dersAdi, StringComparison.OrdinalIgnoreCase)
                             && d.SinifSeviyesi == sinif)
                    .ToList();

                foreach (var sube in subeler)
                {
                    _sablonSonuclari.Add(new SinavAtama
                    {
                        DersAdi = sube.DersAdi,
                        BolumAdi = sube.BolumAdi,
                        SinifSeviyesi = sube.SinifSeviyesi,
                        Kontenjan = sube.Kontenjan,
                        Sube = sube.Sube,
                        OgretimElemani = sube.OgretimElemani,
                        Gun = gun,
                        Saat = bilgi.Saat,
                        AtananSalonlar = new List<SalonAtama>()
                    });
                }
                lblDurum.Text = $"➕ '{dersAdi}' şablona eklendi ({subeler.Count} şube — {gun:dd.MM.yyyy} {bilgi.Saat}).";
                lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
            }

            _sablonDuzenlendi = true;
            SablonYenile();
        }

        /// <summary>A/B/C kontenjan ve INT not hücrelerinin düzenlenmesini işler.</summary>
        private void DgvSablon_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = dgvSablon.Columns[e.ColumnIndex];
            var row = dgvSablon.Rows[e.RowIndex];
            if (!(row.Tag is SablonSatirBilgi bilgi) || _sablonSonuclari == null) return;

            int gunIdx = SablonGunIndeksi(col.Name);
            if (gunIdx < 0) return;

            string ham = row.Cells[e.ColumnIndex].Value?.ToString()?.Trim();

            // ── D şubesi (eski "INT"): serbest not, modele dokunmaz ──
            if (col.Name.EndsWith("_D"))
            {
                string anahtar = IntNotAnahtari(gunIdx, bilgi);
                if (string.IsNullOrEmpty(ham)) _sablonIntNotlari.Remove(anahtar);
                else _sablonIntNotlari[anahtar] = ham;
                return;
            }

            // ── A / B / C: şube kontenjanı düzenleme ──
            int subeSira = col.Name.EndsWith("_A") ? 0 : col.Name.EndsWith("_B") ? 1 : col.Name.EndsWith("_C") ? 2 : -1;
            if (subeSira < 0) return;

            bilgi.GunGruplari.TryGetValue(gunIdx, out var grup);
            if (grup == null || subeSira >= grup.Count)
            {
                // Bu sütunda şube yok — girilen değeri geri al
                if (!string.IsNullOrEmpty(ham))
                {
                    MessageBox.Show("Bu hücrede düzenlenecek bir şube yok.\nÖnce 'Ders Adı' sütunundan ders yerleştirin.",
                        "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    SablonYenile();
                }
                return;
            }

            var atama = grup[subeSira];

            if (!int.TryParse(ham, out int yeniKontenjan) || yeniKontenjan <= 0)
            {
                MessageBox.Show("Kontenjan pozitif bir tam sayı olmalıdır!", "Geçersiz Değer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SablonYenile();
                return;
            }

            if (atama.Kontenjan == yeniKontenjan) return;

            // Şablon modelini güncelle
            atama.Kontenjan = yeniKontenjan;

            // _dersListesi'ndeki ilgili Ders nesnesine de yansıt (DgvDersler_CellEndEdit benzeri)
            var ders = _dersListesi.FirstOrDefault(d =>
                d.BolumAdi.Equals(atama.BolumAdi, StringComparison.OrdinalIgnoreCase) &&
                d.DersAdi.Equals(atama.DersAdi, StringComparison.OrdinalIgnoreCase) &&
                d.SinifSeviyesi == atama.SinifSeviyesi &&
                d.Sube.Equals(atama.Sube, StringComparison.OrdinalIgnoreCase));
            if (ders != null)
            {
                ders.Kontenjan = yeniKontenjan;
                DersGridiGuncelle();
            }

            _sablonDuzenlendi = true;
            SablonYenile();
            lblDurum.Text = $"✏ '{atama.DersAdi}' (Şube {atama.Sube}) kontenjanı {yeniKontenjan} olarak güncellendi.";
            lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
        }

        /// <summary>
        /// Şablon grid'ini yeniden kurar, kaydırma konumunu korur ve doğrulamayı çalıştırır.
        /// Hücre olayları (CellValueChanged vb.) içinden güvenle çağrılabilmesi için
        /// yeniden kurulum BeginInvoke ile mesaj kuyruğuna ertelenir (reentrancy koruması).
        /// </summary>
        private void SablonYenile()
        {
            int ilkSatir = dgvSablon.FirstDisplayedScrollingRowIndex;
            int ilkSutun = dgvSablon.FirstDisplayedScrollingColumnIndex;

            BeginInvoke(new Action(() =>
            {
                SablonGorunumuOlustur();
                SablonDogrulaVeBoya();

                try
                {
                    if (ilkSatir >= 0 && ilkSatir < dgvSablon.Rows.Count)
                        dgvSablon.FirstDisplayedScrollingRowIndex = ilkSatir;
                    if (ilkSutun >= 0 && ilkSutun < dgvSablon.Columns.Count && !dgvSablon.Columns[ilkSutun].Frozen)
                        dgvSablon.FirstDisplayedScrollingColumnIndex = ilkSutun;
                }
                catch { /* kaydırma geri yüklenemezse önemli değil */ }
            }));
        }

        /// <summary>
        /// Şablonun tamamını SablonDogrulayici ile kontrol eder; ihlalli hücreleri
        /// kırmızıya boyar ve tooltip'e ihlal mesajlarını yazar. ENGELLEYİCİ DEĞİLDİR.
        /// </summary>
        private void SablonDogrulaVeBoya()
        {
            if (_sablonDogrulayici == null || _sablonSonuclari == null || dgvSablon.Rows.Count == 0) return;

            var ihlaller = _sablonDogrulayici.Dogrula(_sablonSonuclari);

            // Atama → ihlal mesajları haritası
            var ihlalMap = new Dictionary<SinavAtama, List<string>>();
            foreach (var ihlal in ihlaller)
            {
                if (!ihlalMap.TryGetValue(ihlal.Atama, out var liste))
                    ihlalMap[ihlal.Atama] = liste = new List<string>();
                liste.Add(ihlal.Mesaj);
            }

            var kirmizi = Color.FromArgb(243, 139, 168);
            var koyu = Color.FromArgb(30, 30, 46);

            foreach (DataGridViewRow row in dgvSablon.Rows)
            {
                if (!(row.Tag is SablonSatirBilgi bilgi)) continue;

                foreach (var kvp in bilgi.GunGruplari)
                {
                    var grup = kvp.Value;
                    if (grup == null) continue;

                    var mesajlar = grup
                        .Where(a => ihlalMap.ContainsKey(a))
                        .SelectMany(a => ihlalMap[a])
                        .Distinct()
                        .ToList();
                    if (mesajlar.Count == 0) continue;

                    string tip = "⚠ " + string.Join("\n⚠ ", mesajlar);
                    foreach (var ek in new[] { "_Sinif", "_Ders", "_A", "_B", "_C" })
                    {
                        var cell = row.Cells[$"g{kvp.Key}{ek}"];
                        cell.Style.BackColor = kirmizi;
                        cell.Style.ForeColor = koyu;
                        cell.ToolTipText = tip;
                    }
                }
            }

            if (ihlaller.Count > 0)
            {
                lblDurum.Text = $"⚠ Şablonda {ihlaller.Count} kural ihlali var (kırmızı hücreler) — engellemez, bilerek bırakabilirsiniz.";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
            }
        }

        private void SonuclariGoster(List<SinavAtama> sonuclar)
        {
            dgvSonuc.Columns.Clear(); dgvSonuc.Rows.Clear();
            dgvSonuc.Columns.Add("DersAdi", "Ders Adı"); dgvSonuc.Columns.Add("Bolum", "Bölüm");
            dgvSonuc.Columns.Add("Sinif", "Sınıf"); dgvSonuc.Columns.Add("Sube", "Şube");
            dgvSonuc.Columns.Add("Kontenjan", "Kontenjan"); dgvSonuc.Columns.Add("Gun", "Gün");
            dgvSonuc.Columns.Add("Saat", "Saat"); dgvSonuc.Columns.Add("AtananSalonlar", "Atanan Salon(lar)");
            dgvSonuc.Columns["DersAdi"].FillWeight = 18; dgvSonuc.Columns["Bolum"].FillWeight = 14;
            dgvSonuc.Columns["Sinif"].FillWeight = 7; dgvSonuc.Columns["Sube"].FillWeight = 7;
            dgvSonuc.Columns["Kontenjan"].FillWeight = 8; dgvSonuc.Columns["Gun"].FillWeight = 16;
            dgvSonuc.Columns["Saat"].FillWeight = 8; dgvSonuc.Columns["AtananSalonlar"].FillWeight = 22;

            var gunAdlari = new Dictionary<DayOfWeek, string> { { DayOfWeek.Monday, "Pazartesi" }, { DayOfWeek.Tuesday, "Salı" }, { DayOfWeek.Wednesday, "Çarşamba" }, { DayOfWeek.Thursday, "Perşembe" }, { DayOfWeek.Friday, "Cuma" }, { DayOfWeek.Saturday, "Cumartesi" }, { DayOfWeek.Sunday, "Pazar" } };
            foreach (var a in sonuclar)
            {
                string g = $"{gunAdlari[a.Gun.DayOfWeek]} ({a.Gun:dd.MM.yyyy})";
                dgvSonuc.Rows.Add(a.DersAdi, a.BolumAdi, $"{a.SinifSeviyesi}. Sınıf", a.Sube, a.Kontenjan, g, a.Saat, a.AtananSalonlarText);
            }
            for (int i = 0; i < dgvSonuc.Rows.Count; i++) { if (i % 2 == 1) dgvSonuc.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(55, 56, 75); }
        }

        // =============================================
        // MATRİS GÖRÜNÜM (UI — KATEGORİZE GÖSTERİM)
        // =============================================

        /// <summary>
        /// Sınav dağıtım sonuçlarını matris formatında dgvMatris'e doldurur.
        /// Sütunlar: Bölüm | Sınıf | Tarih | Saat | Ders | Kontenjan | [Salon1] | [Salon2] | ...
        /// Her şube ayrı satır — ilgili salon sütununa atanan öğrenci sayısı yazılır.
        /// </summary>
        private void MatrisGorunumuOlustur(List<SinavAtama> sonuclar, List<Salon> salonlar)
        {
            _isUpdating = true;
            try
            {
                dgvMatris.SuspendLayout();
                dgvMatris.Columns.Clear();
                dgvMatris.Rows.Clear();

                // Satır şablonunu BAZ yüksekliğe sıfırla — zoomlu yeniden kurulumda
                // zoomlu değerin baz olarak kaydedilip kümülatif büyümesini önler
                dgvMatris.RowTemplate.Height = 30;
                dgvMatris.ColumnHeadersHeight = 40;

                if (sonuclar == null || sonuclar.Count == 0 || salonlar == null || salonlar.Count == 0)
                {
                    dgvMatris.ResumeLayout();
                    return;
                }

                // Salonları alfabetik sırala
                var siraliSalonlar = salonlar.OrderBy(s => s.SalonKodu).ToList();
                var salonKodlari = siraliSalonlar.Select(s => s.SalonKodu).ToList();

                // Sabit sütunları ekle
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bolum", HeaderText = "Bölüm", Width = 155, ReadOnly = true, Frozen = true });
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sinif", HeaderText = "Sınıf", Width = 50, ReadOnly = true, Frozen = true });
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tarih", HeaderText = "Tarih", Width = 110, ReadOnly = true, Frozen = true });
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Saat", HeaderText = "Saat", Width = 47, ReadOnly = true, Frozen = true });
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ders", HeaderText = "Dersler", Width = 145, ReadOnly = true, Frozen = true });
                dgvMatris.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kontenjan", HeaderText = "Kont.", Width = 47, ReadOnly = true, Frozen = true });

                // Salon sütunlarını ekle
                foreach (var salonKodu in salonKodlari)
                {
                    var salon = siraliSalonlar.First(s => s.SalonKodu == salonKodu);
                    var salonKolon = new DataGridViewTextBoxColumn
                    {
                        Name = $"salon_{salonKodu.Replace("-", "_").Replace(" ", "_")}",
                        HeaderText = $"{salonKodu}\n({salon.Kapasite})",
                        Width = 50,
                        ReadOnly = false,   // Salon hücreleri elle düzenlenebilir
                        Tag = salon          // CellEndEdit'te salon kodu/kapasitesine erişim için
                    };
                    // Dar sütuna sığması için salon başlığı küçük fontlu ve ortalanmış
                    salonKolon.HeaderCell.Style.Font = new Font("Segoe UI", MatrisSalonBaslikFontBoyutu, FontStyle.Bold);
                    salonKolon.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    salonKolon.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    dgvMatris.Columns.Add(salonKolon);
                }

                // Günler Türkçe
                var gunAdlari = new Dictionary<DayOfWeek, string>
                {
                    {DayOfWeek.Monday,"Pzt"},{DayOfWeek.Tuesday,"Sal"},{DayOfWeek.Wednesday,"Çar"},
                    {DayOfWeek.Thursday,"Per"},{DayOfWeek.Friday,"Cum"},{DayOfWeek.Saturday,"Cmt"},{DayOfWeek.Sunday,"Paz"}
                };

                // Sonuçları sırala
                var siraliSonuclar = sonuclar
                    .OrderBy(s => s.BolumAdi)
                    .ThenBy(s => s.SinifSeviyesi)
                    .ThenBy(s => s.Gun)
                    .ThenBy(s => s.Saat)
                    .ThenBy(s => s.DersAdi)
                    .ThenBy(s => s.Sube)
                    .ToList();

                // Filtre combobox'larını doldur
                cmbFiltreBolum.Items.Clear();
                cmbFiltreBolum.Items.Add("Tümü");
                var bolumler = sonuclar.Select(s => s.BolumAdi).Distinct().OrderBy(b => b).ToList();
                foreach (var b in bolumler)
                    cmbFiltreBolum.Items.Add(b);
                cmbFiltreBolum.SelectedIndex = 0;

                cmbFiltreSinif.Items.Clear();
                cmbFiltreSinif.Items.Add("Tümü");
                var siniflar = sonuclar.Select(s => s.SinifSeviyesi).Distinct().OrderBy(s => s).ToList();
                foreach (var s in siniflar)
                    cmbFiltreSinif.Items.Add($"{s}. Sınıf");
                cmbFiltreSinif.SelectedIndex = 0;

                // Satırları doldur
                string oncekiBolumSinif = "";
                int satirIdx = 0;

                foreach (var atama in siraliSonuclar)
                {
                    string mevcutBolumSinif = $"{atama.BolumAdi}|{atama.SinifSeviyesi}";

                    int rowIdx = dgvMatris.Rows.Add();
                    var row = dgvMatris.Rows[rowIdx];

                    // Tag'e bölüm/sınıf bilgisi (filtreleme) ve atama referansı (düzenleme) kaydet
                    row.Tag = new MatrisSatirBilgi { BolumAdi = atama.BolumAdi, SinifSeviyesi = atama.SinifSeviyesi, Atama = atama };

                    row.Cells["Bolum"].Value = atama.BolumAdi;
                    row.Cells["Sinif"].Value = $"{atama.SinifSeviyesi}";
                    row.Cells["Tarih"].Value = $"{gunAdlari[atama.Gun.DayOfWeek]} {atama.Gun:dd.MM.yyyy}";
                    row.Cells["Saat"].Value = atama.Saat;
                    row.Cells["Ders"].Value = atama.DersEtiketi;
                    row.Cells["Kontenjan"].Value = atama.Kontenjan;

                    // Salon sütunlarını doldur
                    if (atama.AtananSalonlar != null)
                    {
                        foreach (var salonAtama in atama.AtananSalonlar)
                        {
                            int sIdx = salonKodlari.IndexOf(salonAtama.SalonKodu);
                            if (sIdx >= 0)
                            {
                                string colName = $"salon_{salonAtama.SalonKodu.Replace("-", "_").Replace(" ", "_")}";
                                // SADE GÖRÜNÜM: kutucukta YALNIZCA atanan şube ismi görünür
                                // (öğrenci sayısı UI'dan kaldırıldı; Excel çıktısında durur)
                                row.Cells[colName].Value = string.IsNullOrWhiteSpace(salonAtama.Sube)
                                    ? (object)salonAtama.AtananOgrenciSayisi   // şube bilgisi yoksa sayı göster
                                    : salonAtama.Sube;
                                row.Cells[colName].Style.BackColor = Color.FromArgb(166, 227, 161);
                                row.Cells[colName].Style.ForeColor = Color.FromArgb(30, 30, 46);
                                row.Cells[colName].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                            }
                        }
                    }

                    // Bölüm/sınıf grupları arasında renk farklılığı
                    if (mevcutBolumSinif != oncekiBolumSinif && !string.IsNullOrEmpty(oncekiBolumSinif))
                        satirIdx = 0;

                    if (satirIdx % 2 == 1)
                        row.DefaultCellStyle.BackColor = Color.FromArgb(55, 56, 75);

                    oncekiBolumSinif = mevcutBolumSinif;
                    satirIdx++;
                }

                // ── TAM LİSTE (KURAL 6): takvimdeki her gün+saat+bölüm kombinasyonu için,
                //    sınav atanmamış olsa bile BOŞ satır eklenir — dolu satır filtrelenmez.
                if (_takvimSlotlari != null && _takvimSlotlari.Count > 0)
                {
                    string[] tumBolumler = { "BİL", "EEM", "INS", "MAK", "MEK", "MEM" };
                    var doluKombinasyonlar = new HashSet<string>(sonuclar
                        .Select(s => $"{s.BolumAdi?.Trim().ToUpperInvariant()}|{s.Gun.Date:yyyy-MM-dd}|{s.Saat}"));

                    foreach (var gunKvp in _takvimSlotlari.OrderBy(k => k.Key))
                        foreach (var saat in gunKvp.Value.OrderBy(x => x))
                            foreach (var bolum in tumBolumler)
                            {
                                if (doluKombinasyonlar.Contains($"{bolum}|{gunKvp.Key.Date:yyyy-MM-dd}|{saat}"))
                                    continue;

                                int bosIdx = dgvMatris.Rows.Add();
                                var bosRow = dgvMatris.Rows[bosIdx];
                                bosRow.Tag = new MatrisSatirBilgi { BolumAdi = bolum, SinifSeviyesi = 0, Atama = null };
                                bosRow.Cells["Bolum"].Value = bolum;
                                bosRow.Cells["Tarih"].Value = $"{gunAdlari[gunKvp.Key.DayOfWeek]} {gunKvp.Key:dd.MM.yyyy}";
                                bosRow.Cells["Saat"].Value = saat;
                                bosRow.DefaultCellStyle.ForeColor = Color.FromArgb(127, 132, 156); // soluk — boş slot
                            }
                }

                // 1. Otomatik sütun genişletme modunu kapatıyoruz (Bu mod açıkken scrollbar çıkmaz, her şeyi ekrana sıkıştırır)
                dgvMatris.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

                // 2. Tablo, sekmenin kendisine ayrılan TÜM alanı doldursun (pencere büyüyünce grid de büyür).
                //    Sütunlar toplamı alandan genişse Dock=Fill ile de yatay scrollbar sorunsuz çıkar.
                dgvMatris.Dock = DockStyle.Fill;

                // 3. Çubukları kesin olarak her iki yönde de aktif ediyoruz
                dgvMatris.ScrollBars = ScrollBars.Both;

                // 4. Sütunların çok dar kalıp yazıların gizlenmesini engellemek için her salon sütununa minimum genişlik ver
                foreach (DataGridViewColumn col in dgvMatris.Columns)
                {
                    if (col.Name.StartsWith("salon_"))
                    {
                        col.Width = 50; // Başlangıç genişliği — Ctrl+Tekerlek ile zoom yapılınca orantılı büyür
                    }
                }

                // 5. Zoom için baz (referans) genişlik ve yükseklikleri kaydet
                _matrisBazSutunGenislikleri.Clear();
                foreach (DataGridViewColumn col in dgvMatris.Columns)
                    _matrisBazSutunGenislikleri[col.Name] = col.Width;
                _matrisBazSatirYuksekligi = dgvMatris.RowTemplate.Height;

                // Kullanıcı daha önce zoom yaptıysa aynı zoom seviyesini yeni tabloya da uygula
                if (Math.Abs(_matrisFontBoyutu - MatrisBazFontBoyutu) > 0.01F)
                    MatrisZoomUygula();

                dgvMatris.ResumeLayout();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Ctrl + fare tekerleği ile matris tablosunda zoom in/out.
        /// Yukarı = büyüt, aşağı = küçült (font 6–16 arası).
        /// </summary>
        private void DgvMatris_MouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) != Keys.Control) return;

            // Ctrl basılıyken tablonun kaymasını engelle
            if (e is HandledMouseEventArgs he)
                he.Handled = true;

            float yeniBoyut = _matrisFontBoyutu + (e.Delta > 0 ? 1F : -1F);
            yeniBoyut = Math.Max(MatrisMinFontBoyutu, Math.Min(MatrisMaxFontBoyutu, yeniBoyut));
            if (Math.Abs(yeniBoyut - _matrisFontBoyutu) < 0.01F) return;

            _matrisFontBoyutu = yeniBoyut;
            MatrisZoomUygula();
        }

        /// <summary>
        /// Mevcut zoom seviyesini (font boyutunu) matris tablosuna uygular;
        /// sütun genişlikleri ve satır yükseklikleri orantılı olarak ölçeklenir.
        /// </summary>
        private void MatrisZoomUygula()
        {
            if (dgvMatris.Columns.Count == 0) return;

            float oran = _matrisFontBoyutu / MatrisBazFontBoyutu;

            dgvMatris.SuspendLayout();
            try
            {
                dgvMatris.DefaultCellStyle.Font = new Font("Segoe UI", _matrisFontBoyutu);
                dgvMatris.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", _matrisFontBoyutu, FontStyle.Bold);

                // Salon başlıkları baz olarak küçük fontludur; zoom ile orantılı ölçeklenir
                float salonBaslikBoyutu = Math.Max(5F, MatrisSalonBaslikFontBoyutu * oran);

                // Sütun genişliklerini baz genişlik üzerinden ölçekle (kümülatif yuvarlama hatası olmaz)
                foreach (DataGridViewColumn col in dgvMatris.Columns)
                {
                    if (_matrisBazSutunGenislikleri.TryGetValue(col.Name, out int bazGenislik))
                        col.Width = Math.Max(20, (int)Math.Round(bazGenislik * oran));

                    if (col.Name.StartsWith("salon_"))
                        col.HeaderCell.Style.Font = new Font("Segoe UI", salonBaslikBoyutu, FontStyle.Bold);
                }

                // Başlık satırı yüksekliğini de orantıla (baz: 40)
                dgvMatris.ColumnHeadersHeight = Math.Max(24, (int)Math.Round(40 * oran));

                // Satır yüksekliklerini ölçekle
                int bazYukseklik = _matrisBazSatirYuksekligi > 0 ? _matrisBazSatirYuksekligi : 22;
                int yeniYukseklik = Math.Max(14, (int)Math.Round(bazYukseklik * oran));
                dgvMatris.RowTemplate.Height = yeniYukseklik;

                foreach (DataGridViewRow row in dgvMatris.Rows)
                {
                    row.Height = yeniYukseklik;

                    // Özel fontlu hücreleri (ör. yeşil salon hücreleri, bold) yeni boyuta taşı
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell.HasStyle && cell.Style.Font != null)
                            cell.Style.Font = new Font(cell.Style.Font.FontFamily, _matrisFontBoyutu, cell.Style.Font.Style);
                    }
                }
            }
            finally
            {
                dgvMatris.ResumeLayout();
            }
        }

        /// <summary>
        /// Matris görünümünde bölüm/sınıf filtresini uygular.
        /// </summary>
        private void MatrisFiltreUygula()
        {
            if (_isUpdating || dgvMatris.Rows.Count == 0) return;

            string secilenBolum = cmbFiltreBolum.SelectedItem?.ToString() ?? "Tümü";
            string secilenSinifStr = cmbFiltreSinif.SelectedItem?.ToString() ?? "Tümü";

            int secilenSinif = -1;
            if (secilenSinifStr != "Tümü")
            {
                string temiz = secilenSinifStr.Replace(".", "").Replace("Sınıf", "").Replace(" ", "").Trim();
                int.TryParse(temiz, out secilenSinif);
            }

            dgvMatris.SuspendLayout();
            foreach (DataGridViewRow row in dgvMatris.Rows)
            {
                if (row.Tag is MatrisSatirBilgi bilgi)
                {
                    bool bolumUygun = secilenBolum == "Tümü" || bilgi.BolumAdi == secilenBolum;
                    bool sinifUygun = secilenSinif == -1 || bilgi.SinifSeviyesi == secilenSinif;
                    row.Visible = bolumUygun && sinifUygun;
                }
            }
            dgvMatris.ResumeLayout();
        }

        /// <summary>
        /// Matris görünümünde bir salon hücresi elle düzenlendiğinde çalışır.
        /// Girilen değer _sonSonuclar içindeki SinavAtama.AtananSalonlar listesine işlenir;
        /// böylece Excel çıktısı da güncellenen haliyle alınır.
        /// Hücre boş bırakılır veya 0 girilirse salon ataması silinir.
        /// </summary>
        private void DgvMatris_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = dgvMatris.Columns[e.ColumnIndex];
            if (!(col.Tag is Salon salon)) return; // sadece salon sütunları

            var row = dgvMatris.Rows[e.RowIndex];
            if (!(row.Tag is MatrisSatirBilgi bilgi) || bilgi.Atama == null) return;

            var atama = bilgi.Atama;
            var cell = row.Cells[e.ColumnIndex];
            string ham = cell.Value?.ToString()?.Trim();
            var mevcut = atama.AtananSalonlar?.FirstOrDefault(s => s.SalonKodu == salon.SalonKodu);

            // Boş veya 0 → salon atamasını sil
            if (string.IsNullOrEmpty(ham) || ham == "0")
            {
                if (mevcut != null) atama.AtananSalonlar.Remove(mevcut);
                cell.Value = null;
                cell.Style.BackColor = Color.Empty;
                cell.Style.ForeColor = Color.Empty;
                cell.Style.Font = null;
                MatrisDuzenlemeSonrasi(atama);
                return;
            }

            // Hücre "25 (A)" formatında şube etiketi içerebilir — baştaki sayı esas alınır
            var sayiEslesme = System.Text.RegularExpressions.Regex.Match(ham, @"^\s*(\d+)");
            if (!sayiEslesme.Success || !int.TryParse(sayiEslesme.Groups[1].Value, out int yeniDeger) || yeniDeger < 0)
            {
                MessageBox.Show("Lütfen pozitif bir tam sayı giriniz (silmek için hücreyi boşaltın veya 0 girin).",
                    "Geçersiz Değer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // Eski değere dön (kaynak: model)
                cell.Value = mevcut?.AtananOgrenciSayisi.ToString();
                return;
            }

            // Modeli güncelle
            if (mevcut != null)
                mevcut.AtananOgrenciSayisi = yeniDeger;
            else
            {
                atama.AtananSalonlar ??= new List<SalonAtama>();
                atama.AtananSalonlar.Add(new SalonAtama
                {
                    SalonKodu = salon.SalonKodu,
                    SalonKapasitesi = salon.Kapasite,
                    AtananOgrenciSayisi = yeniDeger
                });
            }

            // Hücre stili: kapasite aşılırsa turuncu uyarı, normalse yeşil
            cell.Value = yeniDeger;
            bool kapasiteAsimi = yeniDeger > salon.Kapasite;
            cell.Style.BackColor = kapasiteAsimi ? Color.FromArgb(250, 179, 135) : Color.FromArgb(166, 227, 161);
            cell.Style.ForeColor = Color.FromArgb(30, 30, 46);
            cell.Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            MatrisDuzenlemeSonrasi(atama);

            if (kapasiteAsimi)
            {
                lblDurum.Text = $"⚠ {salon.SalonKodu} kapasitesi ({salon.Kapasite}) aşıldı: {yeniDeger} kişi girildi.";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        /// <summary>Matris düzenlemesi sonrası Sınav Sonuçları sekmesini modelle senkronlar.</summary>
        private void MatrisDuzenlemeSonrasi(SinavAtama atama)
        {
            if (_sonSonuclar != null)
                SonuclariGoster(_sonSonuclar);

            lblDurum.Text = $"✏ '{atama.DersAdi} ({atama.Sube})' salon dağılımı güncellendi — Excel çıktısı bu haliyle alınacak.";
            lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
        }

        // =============================================
        // MATRİS EXCEL EXPORT
        // =============================================

        // Adım 6: gözetmen atamasından sonra düzenlenmek üzere taslak çıktı
        private void BtnMatrisExport_Click(object sender, EventArgs e)
            => MatrisDosyasiKaydet("SinavProgrami_GozetmenliTaslak", taslak: true);

        // Adım 7 sonu: elle düzenlenmiş dosya geri yüklendikten sonra final çıktı
        private void BtnFinalExport_Click(object sender, EventArgs e)
            => MatrisDosyasiKaydet("SinavProgrami_FINAL", taslak: false);

        /// <summary>
        /// Güncel <see cref="_sonSonuclar"/> ile çok sayfalı matris Excel'ini üretir.
        /// <paramref name="taslak"/> true ise başarı sonrası 7. adım (geri yükleme) açılır.
        /// </summary>
        private void MatrisDosyasiKaydet(string dosyaOnEki, bool taslak)
        {
            try
            {
                if (_sonSonuclar == null || _sonSonuclar.Count == 0)
                {
                    MessageBox.Show("Dışa aktarılacak sonuç yok.\nÖnce '3) Şablonu Yükle' → '4) Salonları Ata' adımlarını tamamlayın.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Salonları getir
                List<Salon> salonlar;
                if (_cachedSalonlar.Count > 0)
                    salonlar = _cachedSalonlar;
                else
                {
                    try { salonlar = DatabaseHelper.SalonlariGetir(); }
                    catch (Exception dbEx) { MessageBox.Show($"Salon bilgileri alınamadı:\n{dbEx.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                }

                using (var dlg = new SaveFileDialog
                {
                    Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                    FileName = $"{dosyaOnEki}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    Title = taslak ? "Gözetmenli Taslak Excel'i Kaydet" : "Final Sınav Programını Kaydet"
                })
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Matris Excel dosyası oluşturuluyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    // ÖĞRETİM ELEMANI: sonuçlar gözetmen dosyasından yeniden okunmuş olabilir
                    // (o dosyada hoca kolonu yoktur) — kalıcı hoca eşlemesinden burada doldurulur.
                    OgretimElemanlariniDoldur(_sonSonuclar);

                    // Gözetmen listesi de gönderilir — Görev Çizelgesi sayfasında bölüm eşleştirmesi için
                    ExcelMatrisExportHelper.MatrisExcelOlustur(_sonSonuclar, salonlar, dlg.FileName, "", _gozetmenListesi);

                    lblDurum.Text = taslak ? "✅ Gözetmenli taslak kaydedildi — düzenleyip 'Düzenlenmiş Dosyayı Yükle'ye basın."
                                           : "✅ Final sınav programı kaydedildi!";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (taslak) btnGozetmenliYukle.Enabled = true;

                    string ipucu = taslak
                        ? $"\n\nDüzenlemeleri '{GozetmenliSablonOkuyucu.SayfaAdi}' sayfasında yapın " +
                          "(salon hücresi: 1. satır öğrenci sayısı, 2. satır gözetmen adı), " +
                          "sonra 'Düzenlenmiş Dosyayı Yükle' ile geri yükleyin."
                        : "";
                    MessageBox.Show($"Sınav programı başarıyla dışa aktarıldı:\n\n📁 {dlg.FileName}{ipucu}",
                        "Dışa Aktarma Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Matris Excel export hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Excel export başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // =============================================
        // ADIM 7 — DÜZENLENMİŞ GÖZETMENLİ DOSYAYI GERİ YÜKLE
        // =============================================

        private void BtnGozetmenliYukle_Click(object sender, EventArgs e)
        {
            if (_sonSonuclar == null || _sonSonuclar.Count == 0)
            {
                MessageBox.Show("Önce gözetmen atamasını yapıp gözetmenli taslağı çıkartın.", "Uyarı",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new OpenFileDialog
            {
                Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
                Title = "Düzenlenmiş Gözetmenli Dosyayı Seç"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    lblDurum.Text = "⏳ Düzenlenmiş dosya okunuyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    var okuma = GozetmenliSablonOkuyucu.Oku(dlg.FileName);
                    if (okuma.Satirlar.Count == 0)
                    {
                        MessageBox.Show("Dosyadan hiç ders satırı okunamadı.\n\n" + string.Join("\n", okuma.Uyarilar),
                            "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblDurum.Text = "❌ Düzenlenmiş dosya okunamadı!";
                        lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                        return;
                    }

                    int guncellenen = GozetmenliSablonOkuyucu.Uygula(_sonSonuclar, okuma);

                    GozetmenGridiniGuncelle();
                    SonuclariGoster(_sonSonuclar);
                    try { MatrisGorunumuOlustur(_sonSonuclar, _cachedSalonlar); } catch { /* görünüm opsiyonel */ }

                    btnFinalExport.Enabled = guncellenen > 0;

                    lblDurum.Text = $"✅ {guncellenen} sınav satırı güncellendi" +
                                    (okuma.Uyarilar.Count > 0 ? $", {okuma.Uyarilar.Count} uyarı" : "") +
                                    (guncellenen > 0 ? " — 'Final Programı Çıkart'a basın." : "");
                    lblDurum.ForeColor = okuma.Uyarilar.Count > 0 ? Color.FromArgb(249, 226, 175)
                                                                  : Color.FromArgb(166, 227, 161);

                    if (okuma.Uyarilar.Count > 0)
                        MessageBox.Show($"{guncellenen} sınav satırı güncellendi.\n\nUyarılar:\n\n" +
                                        string.Join("\n", okuma.Uyarilar.Take(25)),
                            "Okuma Uyarıları", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dosya okunamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblDurum.Text = "❌ Düzenlenmiş dosya yüklenemedi!";
                    lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                }
            }
        }

        // =============================================
        // TEMİZLEME
        // =============================================

        private void BtnTemizle_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Tüm veriler temizlenecek. Emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _dersListesi.Clear(); _sonSonuclar = null;
                _gozetmenListesi.Clear(); dgvGozetmenler.Rows.Clear();
                dgvDersler.Rows.Clear(); dgvSonuc.Columns.Clear(); dgvSonuc.Rows.Clear();
                dgvMatris.Columns.Clear(); dgvMatris.Rows.Clear();

                // Şablon durumu da temizlenir (aşamalı akış başa döner)
                _sablonSonuclari = null; _sablonDuzenlendi = false; _sablonDogrulayici = null;
                _sablonIntNotlari.Clear();
                dgvSablon.Columns.Clear(); dgvSablon.Rows.Clear();
                btnSablonOlustur.Enabled = false; btnOtomatikDagit.Enabled = false; btnSalonAta.Enabled = false; btnSalonExport.Enabled = false; btnGozetmenAta.Enabled = false; btnMatrisExport.Enabled = false; btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                cmbDersAdi.Text = ""; cmbDersAdi.Items.Clear(); BolumSecimleriniTemizle(); txtSube.Text = "A"; txtDersAra.Text = "";
                numSinifSeviyesi.Value = 1; numOgrenciSayisi.Value = 50;
                lblDurum.Text = "Henüz program oluşturulmadı."; lblDurum.ForeColor = Color.FromArgb(166, 173, 200);

                PrepopulateDefaultCalendar();
                UygulamaliDersGridiniGuncelle();
            }
        }

        /// <summary>
        /// Veritabanındaki TÜM kayıtlı dersleri silerek uygulamayı fabrika ayarlarına döndürür.
        /// </summary>
        private async void BtnVeritabaniSifirla_Click(object sender, EventArgs e)
        {
            try
            {
                var onay = MessageBox.Show(
                    "⚠ DİKKAT!\n\nUyguladaki tüm dersler, takvim ve sonuçlar temizlenecektir!\n" +
                    "Bu işlem geri alınamaz.\n\nDevam etmek istiyor musunuz?",
                    "Uygulamayı Sıfırlama Onayı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (onay != DialogResult.Yes) return;

                lblDurum.Text = "⏳ Uygulama sıfırlanıyor...";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                Application.DoEvents();

                int silinenSayisi = 0;
                try
                {
                    silinenSayisi = await DatabaseHelper.TumKayitliDersleriSilAsync();
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Veritabanı silme hatası (yok sayılabilir): {dbEx.Message}");
                }

                // Tüm lokal verileri temizle
                _dersListesi.Clear();
                _sonSonuclar = null;
                _gozetmenListesi.Clear();
                dgvGozetmenler.Rows.Clear();
                dgvDersler.Rows.Clear();
                dgvSonuc.Columns.Clear(); dgvSonuc.Rows.Clear();
                dgvMatris.Columns.Clear(); dgvMatris.Rows.Clear();

                // Şablon durumu da sıfırlanır (aşamalı akış başa döner)
                _sablonSonuclari = null; _sablonDuzenlendi = false; _sablonDogrulayici = null;
                _sablonIntNotlari.Clear();
                dgvSablon.Columns.Clear(); dgvSablon.Rows.Clear();
                btnSablonOlustur.Enabled = false; btnOtomatikDagit.Enabled = false; btnSalonAta.Enabled = false; btnSalonExport.Enabled = false; btnGozetmenAta.Enabled = false; btnMatrisExport.Enabled = false; btnGozetmenliYukle.Enabled = false; btnFinalExport.Enabled = false;

                cmbDersAdi.Text = ""; cmbDersAdi.Items.Clear();
                BolumSecimleriniTemizle();
                txtSube.Text = "A";
                txtDersAra.Text = "";
                numSinifSeviyesi.Value = 1;
                numOgrenciSayisi.Value = 50;

                PrepopulateDefaultCalendar();
                UygulamaliDersGridiniGuncelle();

                lblDurum.Text = "✅ Uygulama sıfırlandı! Fabrika ayarlarına geri dönüldü.";
                lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                MessageBox.Show(
                    "Uygulama başarıyla sıfırlandı!\n\n✅ Uygulama ilk açılış durumuna döndürüldü.",
                    "Sıfırlama Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sıfırlama hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Sıfırlama başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // =============================================
        // UYGULAMALI DERS METODLARI
        // Uygulamalı işaretlenen dersler sadece UYGULAMALI tipli
        // salonlara (lab/teknik resim) atanır; diğer dersler o salonları kullanamaz.
        // =============================================

        private void InitializeUygulamaliGrid()
        {
            dgvOverbooking.Columns.Clear();
            dgvOverbooking.AutoGenerateColumns = false;

            dgvOverbooking.Columns.Add(new DataGridViewTextBoxColumn { Name = "DersAdi", HeaderText = "Ders Adı", ReadOnly = true, FillWeight = 75 });
            dgvOverbooking.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Uygulamali", HeaderText = "Uygulamalı ✔", ReadOnly = false, FillWeight = 25 });
        }

        private void UygulamaliDersGridiniGuncelle()
        {
            try
            {
                dgvOverbooking.Rows.Clear();

                var dersler = _dersListesi
                    .Select(d => d.DersAdi)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a)
                    .ToList();

                // Arama filtresi (Türkçe karakterlere duyarlı, büyük/küçük harf bağımsız)
                string filtre = txtUygulamaliAra.Text?.Trim();
                var gosterilecekDersler = dersler;
                if (!string.IsNullOrEmpty(filtre))
                {
                    var trCompare = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
                    gosterilecekDersler = dersler
                        .Where(a => trCompare.IndexOf(a, filtre, System.Globalization.CompareOptions.IgnoreCase) >= 0)
                        .ToList();
                }

                // Listede artık bulunmayan dersleri seçimden düş
                // (DİKKAT: filtreli liste değil, TAM liste baz alınır — arama seçimleri silmesin)
                _uygulamaliDersler.RemoveWhere(a => !dersler.Contains(a, StringComparer.OrdinalIgnoreCase));

                foreach (var ders in gosterilecekDersler)
                    dgvOverbooking.Rows.Add(ders, _uygulamaliDersler.Contains(ders));

                // Ders listesi değiştiğinde bölüm listesi de değişmiş olabilir
                HataOraniGridiniGuncelle();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Uygulamalı ders grid güncelleme hatası: {ex.Message}");
            }
        }

        // =============================================
        // HATA ORANI (OVERBOOKING) METODLARI
        // Bölüm bazlı "sınava gelmeyecek kişi" yüzdesi.
        // Örn: 120 kontenjan + %20 → salonlara 96 kişi gibi dağıtılır.
        // =============================================

        private void InitializeHataOraniGrid()
        {
            dgvHataOrani.Columns.Clear();
            dgvHataOrani.AutoGenerateColumns = false;

            dgvHataOrani.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bolum", HeaderText = "Bölüm", ReadOnly = true, FillWeight = 75 });
            dgvHataOrani.Columns.Add(new DataGridViewTextBoxColumn { Name = "Oran", HeaderText = "Hata Oranı % ✏", ReadOnly = false, FillWeight = 25 });
        }

        private void HataOraniGridiniGuncelle()
        {
            try
            {
                // TEKİLLEŞTİRME (BUG FIX): "X MÜHENDİSLİĞİ" ile "X MÜHENDİSLİĞİ BÖLÜMÜ"
                // (veya tire/boşluk farkları) aynı bölümdür. Normalize anahtar üzerinden
                // tekilleştirilir; ders listesindeki yazım öncelikli görünen ad olur.
                var gorulen = new Dictionary<string, string>(StringComparer.Ordinal); // normKey → görünen ad

                foreach (var b in _dersListesi.Select(d => d.BolumAdi)            // önce Excel'deki adlar
                    .Concat(clbBolum.Items.Cast<object>().Select(o => o.ToString())))
                {
                    if (string.IsNullOrWhiteSpace(b)) continue;
                    string ad = b.Trim();
                    string anahtar = BolumNormAnahtar(ad);
                    if (anahtar.Length == 0 || gorulen.ContainsKey(anahtar)) continue;
                    gorulen[anahtar] = ad;
                }

                dgvHataOrani.Rows.Clear();
                foreach (var bolum in gorulen.Values.OrderBy(b => b))
                    dgvHataOrani.Rows.Add(bolum, HataOraniBul(bolum).ToString("0.#"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hata oranı grid güncelleme hatası: {ex.Message}");
            }
        }

        /// <summary>Kaydedilmiş hata oranını normalize bölüm anahtarıyla bulur (yazım farkına dayanıklı).</summary>
        private double HataOraniBul(string bolum)
        {
            if (_bolumHataOranlari.TryGetValue(bolum, out double v)) return v;
            string anahtar = BolumNormAnahtar(bolum);
            foreach (var kv in _bolumHataOranlari)
                if (BolumNormAnahtar(kv.Key) == anahtar) return kv.Value;
            return 0;
        }

        /// <summary>
        /// Bölüm adını karşılaştırma anahtarına indirger: Türkçe karakter sadeleştirilir,
        /// yalnızca harfler tutulur (küçük), sondaki "bolumu"/"bolum" eki atılır.
        /// "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ" ≡ "Bilgisayar Mühendisliği".
        /// </summary>
        private static string BolumNormAnahtar(string bolum)
        {
            if (string.IsNullOrWhiteSpace(bolum)) return "";
            var sb = new StringBuilder();
            foreach (char ch in bolum)
            {
                char c = ch switch
                {
                    'ı' or 'I' or 'İ' => 'i',
                    'ş' or 'Ş' => 's',
                    'ğ' or 'Ğ' => 'g',
                    'ç' or 'Ç' => 'c',
                    'ö' or 'Ö' => 'o',
                    'ü' or 'Ü' => 'u',
                    _ => ch
                };
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            string s = sb.ToString();
            if (s.EndsWith("bolumu")) s = s.Substring(0, s.Length - 6);
            else if (s.EndsWith("bolum")) s = s.Substring(0, s.Length - 5);
            // "ve" bağlacı farkı: "elektrikveelektronik" ≡ "elektrikelektronik"
            s = s.Replace("veelektronik", "elektronik").Replace("vemalzeme", "malzeme");
            // yazım farkı: makina ≡ makine
            s = s.Replace("makina", "makine");
            return s;
        }

        private void DgvHataOrani_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var bolum = dgvHataOrani.Rows[e.RowIndex].Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(bolum)) return;

            string ham = dgvHataOrani.Rows[e.RowIndex].Cells[1].Value?.ToString()?.Replace("%", "").Replace(',', '.').Trim();

            if (double.TryParse(ham, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double oran)
                && oran >= 0 && oran <= 100)
            {
                _bolumHataOranlari[bolum] = oran;
                dgvHataOrani.Rows[e.RowIndex].Cells[1].Value = oran.ToString("0.#");
            }
            else
            {
                MessageBox.Show("Lütfen 0 ile 100 arasında geçerli bir yüzde giriniz.", "Geçersiz Hata Oranı",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                double eski = _bolumHataOranlari.TryGetValue(bolum, out double v) ? v : 0;
                dgvHataOrani.Rows[e.RowIndex].Cells[1].Value = eski.ToString("0.#");
            }
        }

        // Checkbox tıklanır tıklanmaz CellValueChanged tetiklensin diye anında commit
        private void DgvUygulamali_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvOverbooking.IsCurrentCellDirty)
                dgvOverbooking.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvUygulamali_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var dersAdi = dgvOverbooking.Rows[e.RowIndex].Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(dersAdi)) return;

            bool secili = Convert.ToBoolean(dgvOverbooking.Rows[e.RowIndex].Cells[1].Value ?? false);
            if (secili) _uygulamaliDersler.Add(dersAdi);
            else _uygulamaliDersler.Remove(dersAdi);
        }

        // =============================================
        // GÖZETMEN ATAMA MODÜLÜ (ARAŞTIRMA GÖREVLİSİ)
        // =============================================

        private void InitializeGozetmenGrid()
        {
            dgvGozetmenler.Columns.Clear();
            dgvGozetmenler.AutoGenerateColumns = false;

            dgvGozetmenler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kod", HeaderText = "Kod", ReadOnly = true, FillWeight = 10 });
            dgvGozetmenler.Columns.Add(new DataGridViewTextBoxColumn { Name = "AdSoyad", HeaderText = "Ad Soyad", ReadOnly = true, FillWeight = 30 });
            dgvGozetmenler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bolum", HeaderText = "Bölüm", ReadOnly = true, FillWeight = 32 });
            dgvGozetmenler.Columns.Add(new DataGridViewTextBoxColumn { Name = "Agirlik", HeaderText = "Ağırlık ✏", ReadOnly = false, FillWeight = 14 });
            dgvGozetmenler.Columns.Add(new DataGridViewTextBoxColumn { Name = "AtananSinavSayisi", HeaderText = "Sınav", ReadOnly = true, FillWeight = 14 });

            // Düzenlenebilir Ağırlık sütunu görsel olarak ayrışsın
            dgvGozetmenler.Columns["Agirlik"].DefaultCellStyle.BackColor = Color.FromArgb(60, 62, 85);
            dgvGozetmenler.Columns["Agirlik"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvGozetmenler.Columns["AtananSinavSayisi"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        /// <summary>
        /// Gözetmen Excel dosyasını yükler. Format: Sütun 1 = Ad Soyad, Sütun 2 = Bölüm.
        /// </summary>
        private void BtnGozetmenYukle_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "Excel Dosyası (*.xlsx)|*.xlsx|Tüm Dosyalar (*.*)|*.*";
                    dlg.Title = "Gözetmen Listesi Excel Dosyası Seçin";

                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    lblDurum.Text = "⏳ Gözetmen Excel dosyası okunuyor...";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    Application.DoEvents();

                    List<string> uyarilar;
                    var okunanlar = ExcelImportHelper.GozetmenleriOku(dlg.FileName, out uyarilar);

                    if (okunanlar.Count == 0)
                    {
                        MessageBox.Show("Excel dosyasından hiç gözetmen okunamadı.\n\n" +
                            "Desteklenen formatlar:\n" +
                            "• Fakülte GÖREVLER listesi (Bölüm | Kod | Ad Soyad)\n" +
                            "• Basit liste (Sütun 1 = Ad Soyad, Sütun 2 = Bölüm)",
                            "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblDurum.Text = "❌ Gözetmen yüklenemedi.";
                        lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
                        return;
                    }

                    _gozetmenListesi.Clear();
                    _gozetmenListesi.AddRange(okunanlar);
                    GozetmenGridiniGuncelle();

                    lblDurum.Text = $"✅ {okunanlar.Count} gözetmen yüklendi.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);

                    if (uyarilar.Count > 0)
                        MessageBox.Show($"{uyarilar.Count} satır atlandı:\n\n{string.Join("\n", uyarilar.Take(15))}",
                            "Gözetmen Import Uyarıları", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gözetmen Excel import hatası:\n\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Gözetmen import başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        private void GozetmenGridiniGuncelle()
        {
            _isUpdating = true;
            try
            {
                dgvGozetmenler.SuspendLayout();
                dgvGozetmenler.Rows.Clear();
                foreach (var g in _gozetmenListesi)
                    dgvGozetmenler.Rows.Add(g.Kod, g.AdSoyad, g.Bolum, g.Agirlik.ToString("0.0#"), g.AtananSinavSayisi);
                dgvGozetmenler.ResumeLayout();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// Ağırlık sütunu düzenlendiğinde modele yansıtır (0'dan büyük olmalı).
        /// </summary>
        private void DgvGozetmenler_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdating || e.RowIndex < 0 || e.RowIndex >= _gozetmenListesi.Count) return;
            if (dgvGozetmenler.Columns[e.ColumnIndex].Name != "Agirlik") return;

            var gozetmen = _gozetmenListesi[e.RowIndex];
            string ham = dgvGozetmenler.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Replace(',', '.').Trim();

            if (double.TryParse(ham, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double agirlik)
                && agirlik > 0)
            {
                gozetmen.Agirlik = agirlik;
                dgvGozetmenler.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = agirlik.ToString("0.0#");
                lblDurum.Text = $"✏ '{gozetmen.AdSoyad}' ağırlığı {agirlik:0.0#} olarak güncellendi.";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
            }
            else
            {
                MessageBox.Show("Ağırlık 0'dan büyük bir sayı olmalıdır (örn: 1.0, 1.5, 2.0).",
                    "Geçersiz Ağırlık", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dgvGozetmenler.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = gozetmen.Agirlik.ToString("0.0#");
            }
        }

        /// <summary>
        /// Salonlara dağıtılmış sınavlara gözetmen tahsis eder.
        /// </summary>
        private void BtnGozetmenAta_Click(object sender, EventArgs e)
        {
            try
            {
                if (_sonSonuclar == null || _sonSonuclar.Count == 0)
                {
                    MessageBox.Show("Önce 'Gözetmenler için Dosya Yükle' (veya 'Salonları Ata') ile salon atamalarını yükleyin!", "Uyarı",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (_gozetmenListesi.Count == 0)
                {
                    MessageBox.Show("Önce sol paneldeki 'Gözetmen Yükle' ile gözetmen listesini yükleyin!", "Uyarı",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lblDurum.Text = "⏳ Gözetmenler atanıyor...";
                lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                Application.DoEvents();

                var algoritma = new GozetmenAtamaAlgoritmasi();

                // ── Ortak (esnetilebilir) ders listesi ──
                // 1) Manuel seçim yapıldıysa o liste esastır.
                // 2) Değilse ders KODUNA bakılır: kodu bölüm önekiyle (BIL, ELM, INS,
                //    MAK, MEK, MEM) BAŞLAMAYAN dersler ortak sayılır — gözetmen
                //    esnetme/dengeleme yalnızca bunlarda yapılır.
                // 3) Sınav listesi de yoksa null → algoritma otomatik tespit eder.
                IEnumerable<string> gozetmenOrtakDersleri = null;
                if (_ortakSecimYapildi)
                    gozetmenOrtakDersleri = _manuelOrtakDersAdlari;
                else if (_dersListesi.Count > 0)
                    gozetmenOrtakDersleri = _dersListesi
                        .Where(d => KodBolumDisiMi(d.DersKodu))
                        .Select(d => d.DersAdi)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                algoritma.GozetmenleriAta(_sonSonuclar, _gozetmenListesi, gozetmenOrtakDersleri);

                // Gridleri güncelle: gözetmen sayaçları + sonuç tablosu (salonların yanında gözetmen görünür)
                GozetmenGridiniGuncelle();
                SonuclariGoster(_sonSonuclar);

                // Gözetmen atandı → 'Gözetmenli Taslak Çıkart' aktif.
                // Yeniden atama, daha önce yüklenmiş düzenlenmiş dosyayı geçersiz kılar.
                btnMatrisExport.Enabled = true;
                btnGozetmenliYukle.Enabled = false;
                btnFinalExport.Enabled = false;

                if (algoritma.Uyarilar.Count > 0)
                {
                    lblDurum.Text = $"⚠ {algoritma.ToplamAtama} salon-gözetmen ataması yapıldı, {algoritma.Uyarilar.Count} uyarı!";
                    lblDurum.ForeColor = Color.FromArgb(249, 226, 175);
                    MessageBox.Show(
                        $"Gözetmen ataması tamamlandı: {algoritma.ToplamAtama} atama.\n\nUyarılar:\n\n{string.Join("\n", algoritma.Uyarilar.Take(20))}",
                        "Gözetmen Atama Uyarıları", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    lblDurum.Text = $"✅ Gözetmen ataması tamamlandı — {algoritma.ToplamAtama} salon için gözetmen atandı.";
                    lblDurum.ForeColor = Color.FromArgb(166, 227, 161);
                    MessageBox.Show(
                        $"Gözetmen ataması başarıyla tamamlandı!\n\n" +
                        $"🧑‍🏫 {algoritma.ToplamAtama} salon-gözetmen ataması yapıldı.\n" +
                        $"📊 Excel çıktısında salonların yanında gözetmen adları yer alacak.",
                        "Atama Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gözetmen atama hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "❌ Gözetmen ataması başarısız!";
                lblDurum.ForeColor = Color.FromArgb(243, 139, 168);
            }
        }

        // =============================================
        // DİNAMİK TAKVİM METODLARI
        // =============================================

        /// <summary>Bir gün sınav günü yapıldığında otomatik işaretlenen varsayılan saatler.</summary>
        private static readonly string[] VarsayilanSinavSaatleri = { "09:00", "11:00", "13:00", "15:00", "17:00" };

        // ═══════════════════════════════════════════════════════════════
        //  SINAV DÖNEMİ TİPİ — Vize (1 hafta / 7 gün) | Final (2 hafta / 10 gün)
        //  Seçim, takvimi başlangıç tarihinden itibaren otomatik doldurur.
        //  Kullanıcı sonrasında takvimden tek tek gün ekleyip çıkarabilir.
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Takvim panelinin üstüne sınav tipi + başlangıç tarihi kontrollerini yerleştirir.</summary>
        private void SinavTipiKontrolleriniKur()
        {
            var yaziRengi = Color.FromArgb(205, 214, 244);
            var arkaplan = Color.FromArgb(49, 50, 68);

            int satirUst = calendarSinav.Top;          // yeni satır takvimin bulunduğu yere girer
            const int satirYuksekligi = 30;
            const int satirBosluk = 8;
            int kaydirma = satirYuksekligi * 2 + satirBosluk * 2;   // iki satır eklendi

            // ── 1. satır: etiket + tip seçimi ──
            lblSinavTipi = new Label
            {
                Text = "🗓 Sınav Dönemi:",
                Location = new Point(16, satirUst + 4),
                Size = new Size(120, 24),
                ForeColor = Color.FromArgb(249, 226, 175),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            cmbSinavTipi = new ComboBox
            {
                Location = new Point(140, satirUst),
                Size = new Size(220, satirYuksekligi),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = arkaplan,
                ForeColor = yaziRengi,
                Font = new Font("Segoe UI", 9.5F)
            };
            foreach (var t in SinavTipi.Tumu())
                cmbSinavTipi.Items.Add(SinavTipi.Ad(t));
            cmbSinavTipi.SelectedIndex = 0;
            cmbSinavTipi.SelectedIndexChanged += CmbSinavTipi_SelectedIndexChanged;

            // ── 2. satır: başlangıç tarihi + doldur düğmesi ──
            int satir2 = satirUst + satirYuksekligi + satirBosluk;

            dtpSinavBaslangic = new DateTimePicker
            {
                Location = new Point(16, satir2),
                Size = new Size(180, satirYuksekligi),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddDays(1),
                Font = new Font("Segoe UI", 9.5F)
            };

            btnTakvimDoldur = new Button
            {
                Text = "📅 Takvimi Doldur",
                Location = new Point(204, satir2),
                Size = new Size(156, satirYuksekligi),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(137, 180, 250),
                ForeColor = Color.FromArgb(30, 30, 46),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTakvimDoldur.FlatAppearance.BorderSize = 0;
            btnTakvimDoldur.Click += BtnTakvimDoldur_Click;

            // Takvim ve altındaki kontroller aşağı kaydırılır
            foreach (Control c in new Control[] { calendarSinav, chkGunEtkin, flowSaatler })
                if (c != null) c.Top += kaydirma;

            panelSol.Controls.Add(lblSinavTipi);
            panelSol.Controls.Add(cmbSinavTipi);
            panelSol.Controls.Add(dtpSinavBaslangic);
            panelSol.Controls.Add(btnTakvimDoldur);
        }

        /// <summary>
        /// Excel'den yüklenen takvimin gün sayısına bakarak sınav dönemi tipini günceller
        /// ve arayüzdeki seçimi sessizce senkronlar (7+ gün → Final).
        /// </summary>
        private void SinavTipiniTakvimdenSenkronla()
        {
            _sinavTipi = SinavTipi.TahminEt(_takvimSlotlari.Keys);
            if (cmbSinavTipi != null)
            {
                bool eski = _isUpdating;
                _isUpdating = true;
                try { cmbSinavTipi.SelectedIndex = _sinavTipi == SinavDonemTipi.Final ? 1 : 0; }
                finally { _isUpdating = eski; }
            }
        }

        private void CmbSinavTipi_SelectedIndexChanged(object sender, EventArgs e)
        {
            _sinavTipi = cmbSinavTipi.SelectedIndex == 1 ? SinavDonemTipi.Final : SinavDonemTipi.Vize;
            SinavTipiDurumunuYaz();
        }

        private void SinavTipiDurumunuYaz()
        {
            int hedef = SinavTipi.VarsayilanGunSayisi(_sinavTipi);
            int mevcut = _takvimSlotlari.Count;
            string uyari = mevcut == hedef ? "" : $"  ⚠ takvimde {mevcut} gün seçili";
            lblDurum.Text = $"🗓 {SinavTipi.Ad(_sinavTipi)} — hedef {hedef} sınav günü.{uyari}";
            lblDurum.ForeColor = mevcut == hedef
                ? Color.FromArgb(166, 227, 161)
                : Color.FromArgb(249, 226, 175);
        }

        private void BtnTakvimDoldur_Click(object sender, EventArgs e)
        {
            int gunSayisi = SinavTipi.VarsayilanGunSayisi(_sinavTipi);
            var gunler = SinavTipi.GunleriUret(dtpSinavBaslangic.Value.Date, _sinavTipi);
            if (gunler.Count == 0) return;

            string kural = _sinavTipi == SinavDonemTipi.Final
                ? "2 hafta × Pazartesi–Cuma — HAFTA SONLARI BOŞ"
                : "Pazartesi–Pazar — tüm hafta sınav günü";

            // Haftalara göre özet: "1. Hafta: 20.07–24.07"
            var haftaOzet = gunler
                .GroupBy(g => SinavTipi.HaftaNo(g, gunler))
                .OrderBy(g => g.Key)
                .Select(g => $"  {g.Key}. Hafta: {g.Min():dd.MM} – {g.Max():dd.MM}  ({g.Count()} gün)");

            var onay = MessageBox.Show(
                (_takvimSlotlari.Count > 0
                    ? $"Takvimdeki mevcut {_takvimSlotlari.Count} gün silinecek.\n\n"
                    : "") +
                $"{SinavTipi.Ad(_sinavTipi)} — {gunSayisi} sınav günü\n" +
                $"({kural})\n\n" +
                string.Join("\n", haftaOzet) +
                "\n\nDevam edilsin mi?",
                "Takvimi Doldur", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (onay != DialogResult.Yes) return;

            _takvimSlotlari.Clear();
            foreach (var g in gunler)
                _takvimSlotlari[g] = new List<string>(VarsayilanSinavSaatleri);

            UpdateCalendarBoldedDates();
            calendarSinav.SetDate(gunler.First());
            UpdateZamanPaneli(gunler.First());
            SinavTipiDurumunuYaz();
        }

        private void PrepopulateDefaultCalendar()
        {
            // Açılışta seçili tipin (varsayılan: Vize) gün sayısı kadar gün işaretlenir.
            _takvimSlotlari.Clear();
            foreach (var g in SinavTipi.GunleriUret(DateTime.Today.AddDays(1), _sinavTipi))
                _takvimSlotlari[g] = new List<string>(VarsayilanSinavSaatleri);

            UpdateCalendarBoldedDates();
            UpdateZamanPaneli(calendarSinav.SelectionStart.Date);
            SinavTipiDurumunuYaz();
        }

        private void UpdateCalendarBoldedDates()
        {
            calendarSinav.BoldedDates = _takvimSlotlari.Keys.ToArray();
            calendarSinav.UpdateBoldedDates();
        }

        private void CalendarSinav_DateSelected(object sender, DateRangeEventArgs e)
        {
            UpdateZamanPaneli(e.Start.Date);
        }

        private void UpdateZamanPaneli(DateTime seciliTarih)
        {
            _isUpdating = true;
            try
            {
                bool gunEtkin = _takvimSlotlari.ContainsKey(seciliTarih);
                chkGunEtkin.Checked = gunEtkin;
                chkGunEtkin.Text = $"📅 {seciliTarih:dd.MM.yyyy} Sınav Günü Yap";

                flowSaatler.Controls.Clear();
                // 09:00'dan 17:00'a kadar 1 saat aralıklı seçenekler
                var saatSecenekleri = Enumerable.Range(9, 9)
                    .Select(h => $"{h:00}:00")
                    .ToList();

                foreach (var saat in saatSecenekleri)
                {
                    var chk = new CheckBox
                    {
                        Text = saat,
                        AutoSize = true,
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 10F),
                        Tag = saat,
                        Margin = new Padding(6),
                        Enabled = gunEtkin
                    };

                    if (gunEtkin && _takvimSlotlari[seciliTarih].Contains(saat))
                    {
                        chk.Checked = true;
                    }

                    chk.CheckedChanged += HourCheckBox_CheckedChanged;
                    flowSaatler.Controls.Add(chk);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void ChkGunEtkin_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            DateTime seciliTarih = calendarSinav.SelectionStart.Date;
            if (chkGunEtkin.Checked)
            {
                // FİNAL DÖNEMİ: hafta sonları boş bırakılır — elle işaretlemede uyar
                bool haftaSonu = seciliTarih.DayOfWeek == DayOfWeek.Saturday
                              || seciliTarih.DayOfWeek == DayOfWeek.Sunday;
                if (haftaSonu && _sinavTipi == SinavDonemTipi.Final)
                {
                    var onay = MessageBox.Show(
                        $"{seciliTarih:dd.MM.yyyy} bir hafta sonu günü.\n\n" +
                        "Final programında hafta sonları boş bırakılır " +
                        "(2 hafta × Pazartesi–Cuma = 10 sınav günü).\n\n" +
                        "Yine de sınav günü yapılsın mı?",
                        "Hafta Sonu Uyarısı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (onay != DialogResult.Yes)
                    {
                        _isUpdating = true;
                        try { chkGunEtkin.Checked = false; }
                        finally { _isUpdating = false; }
                        return;
                    }
                }

                if (!_takvimSlotlari.ContainsKey(seciliTarih))
                {
                    // Varsayılan saatleri ekle (17:00 dahil)
                    _takvimSlotlari[seciliTarih] = new List<string>(VarsayilanSinavSaatleri);
                    UpdateCalendarBoldedDates();
                }
            }
            else
            {
                if (_takvimSlotlari.ContainsKey(seciliTarih))
                {
                    _takvimSlotlari.Remove(seciliTarih);
                    UpdateCalendarBoldedDates();
                }
            }
            UpdateZamanPaneli(seciliTarih);
            SinavTipiDurumunuYaz();   // seçili gün sayısı hedefle uyumlu mu?
        }

        private void HourCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            DateTime seciliTarih = calendarSinav.SelectionStart.Date;
            if (!_takvimSlotlari.ContainsKey(seciliTarih)) return;

            var chk = (CheckBox)sender;
            string saat = chk.Tag.ToString();

            if (chk.Checked)
            {
                if (!_takvimSlotlari[seciliTarih].Contains(saat))
                {
                    _takvimSlotlari[seciliTarih].Add(saat);
                    _takvimSlotlari[seciliTarih].Sort();
                }
            }
            else
            {
                if (_takvimSlotlari[seciliTarih].Contains(saat))
                {
                    _takvimSlotlari[seciliTarih].Remove(saat);
                }

                // Eğer o günün hiç saati kalmadıysa, o günü takvimden kaldır
                if (_takvimSlotlari[seciliTarih].Count == 0)
                {
                    _takvimSlotlari.Remove(seciliTarih);
                    UpdateCalendarBoldedDates();
                    UpdateZamanPaneli(seciliTarih);
                }
            }
        }

        private void separator_Click(object sender, EventArgs e)
        {

        }

        private void lblZamanBaslik_Click(object sender, EventArgs e)
        {

        }

        private void lblDbDurum_Click(object sender, EventArgs e)
        {

        }

        private void lblOverbooking_Click(object sender, EventArgs e)
        {

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }

    // =============================================
    // YARDIMCI SINIF — Matris satır bilgisi (filtreleme için)
    // =============================================
    internal class MatrisSatirBilgi
    {
        public string BolumAdi { get; set; }
        public int SinifSeviyesi { get; set; }
        public SinavAtama Atama { get; set; }
    }

    // =============================================
    // YARDIMCI SINIF — Şablon grid satır bilgisi
    // Satırın saat + bölüm konumunu ve her gün sütun bloğundaki
    // ders grubunun (şubelerin) şablon atama referanslarını tutar.
    // =============================================
    internal class SablonSatirBilgi
    {
        /// <summary>Satırın ait olduğu saat bloğu (örn. "09:00").</summary>
        public string Saat { get; set; }

        /// <summary>Bölüm kısaltması (BİL, EEM, İNŞ, MAK, MEK, MEM...).</summary>
        public string BolumKisa { get; set; }

        /// <summary>Bölümün tam adı (_dersListesi eşleştirmeleri için).</summary>
        public string BolumTam { get; set; }

        /// <summary>Aynı saat+bölümde birden çok ders varsa bu satırın grup sırası (0 tabanlı).</summary>
        public int GrupSira { get; set; }

        /// <summary>Gün indeksi → o hücredeki ders grubunun atamaları (şube sıralı; boşsa null).</summary>
        public Dictionary<int, List<SinavAtama>> GunGruplari { get; } = new Dictionary<int, List<SinavAtama>>();
    }
}
