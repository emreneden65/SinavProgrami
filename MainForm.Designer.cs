#nullable disable
namespace SinavProgrami
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle9 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle10 = new DataGridViewCellStyle();
            panelSol = new Panel();
            lblDersBaslik = new Label();
            lblSubeKuraliUyari = new Label();
            lblBolum = new Label();
            clbBolum = new CheckedListBox();
            btnHavuzYonetimi = new Button();
            txtDersAra = new TextBox();
            lblSinifSeviyesi = new Label();
            numSinifSeviyesi = new NumericUpDown();
            lblDersAdi = new Label();
            cmbDersAdi = new ComboBox();
            lblSube = new Label();
            txtSube = new TextBox();
            lblOgrenciSayisi = new Label();
            numOgrenciSayisi = new NumericUpDown();
            btnDersEkle = new Button();
            btnDersSil = new Button();
            btnExcelImport = new Button();
            dgvDersler = new DataGridView();
            lblOverbooking = new Label();
            txtUygulamaliAra = new TextBox();
            dgvOverbooking = new DataGridView();
            lblHataOrani = new Label();
            dgvHataOrani = new DataGridView();
            separator = new Label();
            lblZamanBaslik = new Label();
            calendarSinav = new MonthCalendar();
            chkGunEtkin = new CheckBox();
            flowSaatler = new FlowLayoutPanel();
            btnSablonOlustur = new Button();
            btnOtomatikDagit = new Button();
            btnSablonYukle = new Button();
            btnSalonExport = new Button();
            btnGozetmenDosyaYukle = new Button();
            cmbSablonMod = new ComboBox();
            btnSalonAta = new Button();
            btnTemizle = new Button();
            btnMatrisExport = new Button();
            btnGozetmenliYukle = new Button();
            btnFinalExport = new Button();
            btnVeritabaniSifirla = new Button();
            lblDbDurum = new Label();
            lblGozetmenBaslik = new Label();
            btnGozetmenYukle = new Button();
            btnGozetmenAta = new Button();
            dgvGozetmenler = new DataGridView();
            panelSag = new Panel();
            lblSonucBaslik = new Label();
            lblDurum = new Label();
            tabControlSonuc = new TabControl();
            tabSablon = new TabPage();
            dgvSablon = new DataGridView();
            tabSonuclar = new TabPage();
            dgvSonuc = new DataGridView();
            tabMatris = new TabPage();
            dgvMatris = new DataGridView();
            panelMatrisFiltre = new Panel();
            lblFiltreBolum = new Label();
            cmbFiltreBolum = new ComboBox();
            lblFiltreSinif = new Label();
            cmbFiltreSinif = new ComboBox();
            panelSol.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numSinifSeviyesi).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numOgrenciSayisi).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvDersler).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvOverbooking).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvHataOrani).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvGozetmenler).BeginInit();
            panelSag.SuspendLayout();
            tabControlSonuc.SuspendLayout();
            tabSablon.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSablon).BeginInit();
            tabSonuclar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSonuc).BeginInit();
            tabMatris.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvMatris).BeginInit();
            panelMatrisFiltre.SuspendLayout();
            SuspendLayout();
            // 
            // panelSol
            // 
            panelSol.AutoScroll = true;
            panelSol.BackColor = Color.FromArgb(36, 36, 58);
            panelSol.Controls.Add(lblDersBaslik);
            panelSol.Controls.Add(lblSubeKuraliUyari);
            panelSol.Controls.Add(lblBolum);
            panelSol.Controls.Add(clbBolum);
            panelSol.Controls.Add(lblSinifSeviyesi);
            panelSol.Controls.Add(numSinifSeviyesi);
            panelSol.Controls.Add(lblDersAdi);
            panelSol.Controls.Add(cmbDersAdi);
            panelSol.Controls.Add(lblSube);
            panelSol.Controls.Add(txtSube);
            panelSol.Controls.Add(lblOgrenciSayisi);
            panelSol.Controls.Add(numOgrenciSayisi);
            panelSol.Controls.Add(btnDersEkle);
            panelSol.Controls.Add(btnDersSil);
            panelSol.Controls.Add(btnExcelImport);
            panelSol.Controls.Add(btnHavuzYonetimi);
            panelSol.Controls.Add(txtDersAra);
            panelSol.Controls.Add(dgvDersler);
            panelSol.Controls.Add(lblOverbooking);
            panelSol.Controls.Add(txtUygulamaliAra);
            panelSol.Controls.Add(dgvOverbooking);
            panelSol.Controls.Add(lblHataOrani);
            panelSol.Controls.Add(dgvHataOrani);
            panelSol.Controls.Add(separator);
            panelSol.Controls.Add(lblZamanBaslik);
            panelSol.Controls.Add(calendarSinav);
            panelSol.Controls.Add(chkGunEtkin);
            panelSol.Controls.Add(flowSaatler);
            panelSol.Controls.Add(btnSablonOlustur);
            panelSol.Controls.Add(btnOtomatikDagit);
            panelSol.Controls.Add(btnSablonYukle);
            panelSol.Controls.Add(btnSalonExport);
            panelSol.Controls.Add(btnGozetmenDosyaYukle);
            panelSol.Controls.Add(cmbSablonMod);
            panelSol.Controls.Add(btnSalonAta);
            panelSol.Controls.Add(btnTemizle);
            panelSol.Controls.Add(btnMatrisExport);
            panelSol.Controls.Add(btnGozetmenliYukle);
            panelSol.Controls.Add(btnFinalExport);
            panelSol.Controls.Add(btnVeritabaniSifirla);
            panelSol.Controls.Add(lblDbDurum);
            panelSol.Controls.Add(lblGozetmenBaslik);
            panelSol.Controls.Add(btnGozetmenYukle);
            panelSol.Controls.Add(btnGozetmenAta);
            panelSol.Controls.Add(dgvGozetmenler);
            panelSol.Dock = DockStyle.Left;
            panelSol.Location = new Point(0, 0);
            panelSol.Name = "panelSol";
            panelSol.Padding = new Padding(16);
            panelSol.Size = new Size(820, 980);
            panelSol.TabIndex = 1;
            // 
            // lblDersBaslik
            // 
            lblDersBaslik.AutoSize = true;
            lblDersBaslik.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblDersBaslik.ForeColor = Color.FromArgb(137, 180, 250);
            lblDersBaslik.Location = new Point(16, 5);
            lblDersBaslik.Name = "lblDersBaslik";
            lblDersBaslik.Size = new Size(183, 25);
            lblDersBaslik.TabIndex = 0;
            lblDersBaslik.Text = "📚 Ders Giriş Paneli";
            // 
            // lblSubeKuraliUyari
            // 
            lblSubeKuraliUyari.BackColor = Color.FromArgb(249, 226, 175);
            lblSubeKuraliUyari.BorderStyle = BorderStyle.FixedSingle;
            lblSubeKuraliUyari.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblSubeKuraliUyari.ForeColor = Color.FromArgb(30, 30, 46);
            lblSubeKuraliUyari.Location = new Point(16, 30);
            lblSubeKuraliUyari.Name = "lblSubeKuraliUyari";
            lblSubeKuraliUyari.Padding = new Padding(6);
            lblSubeKuraliUyari.Size = new Size(490, 45);
            lblSubeKuraliUyari.TabIndex = 1;
            lblSubeKuraliUyari.Text = "⚠️ ŞUBE KURALI UYARISI:\nAynı dersin farklı şubeleri (A, B...) sistem tarafından otomatik olarak aynı Gün ve Saat slotuna planlanır ancak salon yerleşimleri ayrı ayrı ve bağımsız olarak hesaplanır.";
            // 
            // lblBolum
            // 
            lblBolum.AutoSize = true;
            lblBolum.Location = new Point(16, 80);
            lblBolum.Name = "lblBolum";
            lblBolum.Size = new Size(51, 19);
            lblBolum.TabIndex = 2;
            lblBolum.Text = "Bölüm:";
            //
            // clbBolum
            //
            clbBolum.BackColor = Color.FromArgb(49, 50, 68);
            clbBolum.BorderStyle = BorderStyle.FixedSingle;
            clbBolum.CheckOnClick = true;
            clbBolum.ForeColor = Color.FromArgb(205, 214, 244);
            clbBolum.Items.AddRange(new object[] { "BİLGİSAYAR MÜHENDİSLİĞİ BÖLÜMÜ", "ELEKTRİK-ELEKTRONİK MÜHENDİSLİĞİ", "İNŞAAT MÜHENDİSLİĞİ BÖLÜMÜ", "MAKİNE MÜHENDİSLİĞİ BÖLÜMÜ", "MEKATRONİK MÜHENDİSLİĞİ BÖLÜMÜ", "METALURJİ VE MALZEME MÜHENDİSLİĞİ BÖLÜMÜ" });
            clbBolum.Location = new Point(16, 100);
            clbBolum.Name = "clbBolum";
            clbBolum.Size = new Size(390, 65);
            clbBolum.TabIndex = 3;
            // 
            // lblSinifSeviyesi
            // 
            lblSinifSeviyesi.AutoSize = true;
            lblSinifSeviyesi.Location = new Point(426, 80);
            lblSinifSeviyesi.Name = "lblSinifSeviyesi";
            lblSinifSeviyesi.Size = new Size(37, 19);
            lblSinifSeviyesi.TabIndex = 4;
            lblSinifSeviyesi.Text = "Sınıf:";
            // 
            // numSinifSeviyesi
            // 
            numSinifSeviyesi.BackColor = Color.FromArgb(49, 50, 68);
            numSinifSeviyesi.ForeColor = Color.FromArgb(205, 214, 244);
            numSinifSeviyesi.Location = new Point(426, 100);
            numSinifSeviyesi.Maximum = new decimal(new int[] { 4, 0, 0, 0 });
            numSinifSeviyesi.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numSinifSeviyesi.Name = "numSinifSeviyesi";
            numSinifSeviyesi.Size = new Size(80, 25);
            numSinifSeviyesi.TabIndex = 5;
            numSinifSeviyesi.Value = new decimal(new int[] { 1, 0, 0, 0 });
            // 
            // lblDersAdi
            // 
            lblDersAdi.AutoSize = true;
            lblDersAdi.Location = new Point(16, 172);
            lblDersAdi.Name = "lblDersAdi";
            lblDersAdi.Size = new Size(64, 19);
            lblDersAdi.TabIndex = 6;
            lblDersAdi.Text = "Ders Adı:";
            // 
            // cmbDersAdi
            // 
            cmbDersAdi.BackColor = Color.FromArgb(49, 50, 68);
            cmbDersAdi.FlatStyle = FlatStyle.Flat;
            cmbDersAdi.ForeColor = Color.FromArgb(205, 214, 244);
            cmbDersAdi.Location = new Point(16, 192);
            cmbDersAdi.Name = "cmbDersAdi";
            cmbDersAdi.Size = new Size(310, 25);
            cmbDersAdi.TabIndex = 7;
            // 
            // lblSube
            // 
            lblSube.AutoSize = true;
            lblSube.Location = new Point(340, 172);
            lblSube.Name = "lblSube";
            lblSube.Size = new Size(42, 19);
            lblSube.TabIndex = 8;
            lblSube.Text = "Şube:";
            // 
            // txtSube
            // 
            txtSube.BackColor = Color.FromArgb(49, 50, 68);
            txtSube.BorderStyle = BorderStyle.FixedSingle;
            txtSube.ForeColor = Color.FromArgb(205, 214, 244);
            txtSube.Location = new Point(340, 192);
            txtSube.Name = "txtSube";
            txtSube.Size = new Size(60, 25);
            txtSube.TabIndex = 9;
            txtSube.Text = "A";
            // 
            // lblOgrenciSayisi
            // 
            lblOgrenciSayisi.AutoSize = true;
            lblOgrenciSayisi.Location = new Point(414, 172);
            lblOgrenciSayisi.Name = "lblOgrenciSayisi";
            lblOgrenciSayisi.Size = new Size(97, 19);
            lblOgrenciSayisi.TabIndex = 10;
            lblOgrenciSayisi.Text = "Öğrenci Sayısı:";
            // 
            // numOgrenciSayisi
            // 
            numOgrenciSayisi.BackColor = Color.FromArgb(49, 50, 68);
            numOgrenciSayisi.ForeColor = Color.FromArgb(205, 214, 244);
            numOgrenciSayisi.Location = new Point(414, 192);
            numOgrenciSayisi.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
            numOgrenciSayisi.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numOgrenciSayisi.Name = "numOgrenciSayisi";
            numOgrenciSayisi.Size = new Size(92, 25);
            numOgrenciSayisi.TabIndex = 11;
            numOgrenciSayisi.Value = new decimal(new int[] { 50, 0, 0, 0 });
            // 
            // btnDersEkle
            // 
            btnDersEkle.BackColor = Color.FromArgb(166, 227, 161);
            btnDersEkle.Cursor = Cursors.Hand;
            btnDersEkle.FlatAppearance.BorderSize = 0;
            btnDersEkle.FlatStyle = FlatStyle.Flat;
            btnDersEkle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnDersEkle.ForeColor = Color.FromArgb(30, 30, 46);
            btnDersEkle.Location = new Point(16, 224);
            btnDersEkle.Name = "btnDersEkle";
            btnDersEkle.Size = new Size(112, 35);
            btnDersEkle.TabIndex = 12;
            btnDersEkle.Text = "➕ Ders Ekle";
            btnDersEkle.UseVisualStyleBackColor = false;
            // 
            // btnDersSil
            // 
            btnDersSil.BackColor = Color.FromArgb(243, 139, 168);
            btnDersSil.Cursor = Cursors.Hand;
            btnDersSil.FlatAppearance.BorderSize = 0;
            btnDersSil.FlatStyle = FlatStyle.Flat;
            btnDersSil.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnDersSil.ForeColor = Color.FromArgb(30, 30, 46);
            btnDersSil.Location = new Point(136, 224);
            btnDersSil.Name = "btnDersSil";
            btnDersSil.Size = new Size(110, 35);
            btnDersSil.TabIndex = 13;
            btnDersSil.Text = "🗑 Seçili Sil";
            btnDersSil.UseVisualStyleBackColor = false;
            // 
            // btnExcelImport
            // 
            btnExcelImport.BackColor = Color.FromArgb(249, 226, 175);
            btnExcelImport.Cursor = Cursors.Hand;
            btnExcelImport.FlatAppearance.BorderSize = 0;
            btnExcelImport.FlatStyle = FlatStyle.Flat;
            btnExcelImport.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnExcelImport.ForeColor = Color.FromArgb(30, 30, 46);
            btnExcelImport.Location = new Point(545, 80);
            btnExcelImport.Name = "btnExcelImport";
            btnExcelImport.Size = new Size(250, 40);
            btnExcelImport.TabIndex = 14;
            btnExcelImport.Text = "📂 Sınav Listesi Yükle";
            btnExcelImport.TextAlign = ContentAlignment.MiddleLeft;
            btnExcelImport.UseVisualStyleBackColor = false;
            //
            // btnHavuzYonetimi
            //
            btnHavuzYonetimi.BackColor = Color.FromArgb(198, 160, 246);
            btnHavuzYonetimi.Cursor = Cursors.Hand;
            btnHavuzYonetimi.FlatAppearance.BorderSize = 0;
            btnHavuzYonetimi.FlatStyle = FlatStyle.Flat;
            btnHavuzYonetimi.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnHavuzYonetimi.ForeColor = Color.FromArgb(30, 30, 46);
            btnHavuzYonetimi.Location = new Point(384, 224);
            btnHavuzYonetimi.Name = "btnHavuzYonetimi";
            btnHavuzYonetimi.Size = new Size(122, 35);
            btnHavuzYonetimi.TabIndex = 31;
            btnHavuzYonetimi.Text = "🔗 Havuz Yönetimi";
            btnHavuzYonetimi.UseVisualStyleBackColor = false;
            //
            // txtDersAra
            //
            txtDersAra.BackColor = Color.FromArgb(49, 50, 68);
            txtDersAra.BorderStyle = BorderStyle.FixedSingle;
            txtDersAra.ForeColor = Color.FromArgb(205, 214, 244);
            txtDersAra.Location = new Point(16, 266);
            txtDersAra.Name = "txtDersAra";
            txtDersAra.PlaceholderText = "🔍 Ders veya bölüm ara...";
            txtDersAra.Size = new Size(490, 25);
            txtDersAra.TabIndex = 32;
            // 
            // dgvDersler
            // 
            dgvDersler.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvDersler.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvDersler.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.FromArgb(69, 71, 90);
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dgvDersler.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = Color.FromArgb(49, 50, 68);
            dataGridViewCellStyle2.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle2.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle2.SelectionBackColor = Color.FromArgb(137, 180, 250);
            dataGridViewCellStyle2.SelectionForeColor = Color.FromArgb(30, 30, 46);
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dgvDersler.DefaultCellStyle = dataGridViewCellStyle2;
            dgvDersler.EnableHeadersVisualStyles = false;
            dgvDersler.GridColor = Color.FromArgb(69, 71, 90);
            dgvDersler.Location = new Point(16, 297);
            dgvDersler.Name = "dgvDersler";
            dgvDersler.RowHeadersVisible = false;
            dgvDersler.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvDersler.Size = new Size(490, 65);
            dgvDersler.TabIndex = 15;
            // 
            // lblOverbooking
            // 
            lblOverbooking.AutoSize = true;
            lblOverbooking.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblOverbooking.ForeColor = Color.FromArgb(198, 160, 246);
            lblOverbooking.Location = new Point(16, 365);
            lblOverbooking.Name = "lblOverbooking";
            lblOverbooking.Size = new Size(391, 20);
            lblOverbooking.TabIndex = 16;
            lblOverbooking.Text = "🔬 Uygulamalı Ders Salon Tahsisi (Lab / Teknik Resim)";
            lblOverbooking.Click += lblOverbooking_Click;
            //
            // txtUygulamaliAra
            //
            txtUygulamaliAra.BackColor = Color.FromArgb(49, 50, 68);
            txtUygulamaliAra.BorderStyle = BorderStyle.FixedSingle;
            txtUygulamaliAra.ForeColor = Color.FromArgb(205, 214, 244);
            txtUygulamaliAra.Location = new Point(412, 362);
            txtUygulamaliAra.Name = "txtUygulamaliAra";
            txtUygulamaliAra.PlaceholderText = "🔍 Ders ara...";
            txtUygulamaliAra.Size = new Size(94, 25);
            txtUygulamaliAra.TabIndex = 30;
            //
            // dgvOverbooking
            //
            dgvOverbooking.AllowUserToAddRows = false;
            dgvOverbooking.AllowUserToDeleteRows = false;
            dgvOverbooking.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvOverbooking.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvOverbooking.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = Color.FromArgb(69, 71, 90);
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle3.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            dgvOverbooking.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = Color.FromArgb(49, 50, 68);
            dataGridViewCellStyle4.Font = new Font("Segoe UI", 10F);
            dataGridViewCellStyle4.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(137, 180, 250);
            dataGridViewCellStyle4.SelectionForeColor = Color.FromArgb(30, 30, 46);
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
            dgvOverbooking.DefaultCellStyle = dataGridViewCellStyle4;
            dgvOverbooking.EnableHeadersVisualStyles = false;
            dgvOverbooking.GridColor = Color.FromArgb(69, 71, 90);
            dgvOverbooking.Location = new Point(16, 388);
            dgvOverbooking.Name = "dgvOverbooking";
            dgvOverbooking.RowHeadersVisible = false;
            dgvOverbooking.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvOverbooking.Size = new Size(490, 90);
            dgvOverbooking.TabIndex = 17;
            //
            // lblHataOrani
            //
            lblHataOrani.AutoSize = true;
            lblHataOrani.Enabled = true;
            lblHataOrani.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblHataOrani.ForeColor = Color.FromArgb(250, 179, 135);
            lblHataOrani.Location = new Point(16, 484);
            lblHataOrani.Name = "lblHataOrani";
            lblHataOrani.Size = new Size(420, 20);
            lblHataOrani.TabIndex = 28;
            lblHataOrani.Text = "🎯 Hata Oranı — Bölüm Bazlı Sınava Gelmeyecek Kişi (%)";
            lblHataOrani.Visible = true;
            //
            // dgvHataOrani
            //
            dgvHataOrani.AllowUserToAddRows = false;
            dgvHataOrani.AllowUserToDeleteRows = false;
            dgvHataOrani.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvHataOrani.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvHataOrani.BorderStyle = BorderStyle.None;
            dgvHataOrani.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dgvHataOrani.DefaultCellStyle = dataGridViewCellStyle4;
            dgvHataOrani.EnableHeadersVisualStyles = false;
            dgvHataOrani.Enabled = true;
            dgvHataOrani.GridColor = Color.FromArgb(69, 71, 90);
            dgvHataOrani.Location = new Point(16, 507);
            dgvHataOrani.Name = "dgvHataOrani";
            dgvHataOrani.RowHeadersVisible = false;
            dgvHataOrani.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvHataOrani.Size = new Size(490, 95);
            dgvHataOrani.TabIndex = 29;
            dgvHataOrani.Visible = true;
            // 
            // separator
            // 
            separator.BorderStyle = BorderStyle.Fixed3D;
            separator.Location = new Point(16, 612);
            separator.Name = "separator";
            separator.Size = new Size(490, 2);
            separator.TabIndex = 18;
            separator.Click += separator_Click;
            // 
            // lblZamanBaslik
            // 
            lblZamanBaslik.AutoSize = true;
            lblZamanBaslik.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblZamanBaslik.ForeColor = Color.FromArgb(249, 226, 175);
            lblZamanBaslik.Location = new Point(16, 630);
            lblZamanBaslik.Name = "lblZamanBaslik";
            lblZamanBaslik.Size = new Size(298, 25);
            lblZamanBaslik.TabIndex = 19;
            lblZamanBaslik.Text = "⏰ Sınav Takvimi ve Saat Ayarları";
            lblZamanBaslik.Click += lblZamanBaslik_Click;
            // 
            // calendarSinav
            // 
            calendarSinav.Location = new Point(16, 664);
            calendarSinav.MaxSelectionCount = 1;
            calendarSinav.Name = "calendarSinav";
            calendarSinav.TabIndex = 20;
            calendarSinav.TitleBackColor = Color.FromArgb(137, 180, 250);
            calendarSinav.TitleForeColor = Color.FromArgb(30, 30, 46);
            // 
            // chkGunEtkin
            // 
            chkGunEtkin.Cursor = Cursors.Hand;
            chkGunEtkin.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            chkGunEtkin.ForeColor = Color.FromArgb(166, 227, 161);
            chkGunEtkin.Location = new Point(260, 664);
            chkGunEtkin.Name = "chkGunEtkin";
            chkGunEtkin.Size = new Size(246, 28);
            chkGunEtkin.TabIndex = 21;
            chkGunEtkin.Text = "📅 Sınav Günü Yap";
            // 
            // flowSaatler
            // 
            flowSaatler.AutoScroll = true;
            flowSaatler.BackColor = Color.FromArgb(49, 50, 68);
            flowSaatler.Location = new Point(260, 696);
            flowSaatler.Name = "flowSaatler";
            flowSaatler.Padding = new Padding(6);
            flowSaatler.Size = new Size(246, 130);
            flowSaatler.TabIndex = 22;
            //
            // btnSablonOlustur
            //
            btnSablonOlustur.BackColor = Color.FromArgb(148, 226, 213);
            btnSablonOlustur.Cursor = Cursors.Hand;
            btnSablonOlustur.Enabled = false;
            btnSablonOlustur.FlatAppearance.BorderSize = 0;
            btnSablonOlustur.FlatStyle = FlatStyle.Flat;
            btnSablonOlustur.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSablonOlustur.ForeColor = Color.FromArgb(30, 30, 46);
            btnSablonOlustur.Location = new Point(545, 128);
            btnSablonOlustur.Name = "btnSablonOlustur";
            btnSablonOlustur.Size = new Size(122, 48);
            btnSablonOlustur.TabIndex = 23;
            btnSablonOlustur.Text = "📐 Şablon Oluştur (Boş)";
            btnSablonOlustur.TextAlign = ContentAlignment.MiddleCenter;
            btnSablonOlustur.UseVisualStyleBackColor = false;
            //
            // btnOtomatikDagit
            //
            btnOtomatikDagit.BackColor = Color.FromArgb(250, 179, 135);
            btnOtomatikDagit.Cursor = Cursors.Hand;
            btnOtomatikDagit.Enabled = false;
            btnOtomatikDagit.FlatAppearance.BorderSize = 0;
            btnOtomatikDagit.FlatStyle = FlatStyle.Flat;
            btnOtomatikDagit.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnOtomatikDagit.ForeColor = Color.FromArgb(30, 30, 46);
            btnOtomatikDagit.Location = new Point(673, 128);
            btnOtomatikDagit.Name = "btnOtomatikDagit";
            btnOtomatikDagit.Size = new Size(122, 48);
            btnOtomatikDagit.TabIndex = 37;
            btnOtomatikDagit.Text = "⚙ Şablon Oluştur (Otomatik)";
            btnOtomatikDagit.TextAlign = ContentAlignment.MiddleCenter;
            btnOtomatikDagit.UseVisualStyleBackColor = false;
            //
            // btnSablonYukle
            //
            btnSablonYukle.BackColor = Color.FromArgb(203, 166, 247);
            btnSablonYukle.Cursor = Cursors.Hand;
            btnSablonYukle.FlatAppearance.BorderSize = 0;
            btnSablonYukle.FlatStyle = FlatStyle.Flat;
            btnSablonYukle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSablonYukle.ForeColor = Color.FromArgb(30, 30, 46);
            btnSablonYukle.Location = new Point(545, 192);
            btnSablonYukle.Name = "btnSablonYukle";
            btnSablonYukle.Size = new Size(250, 40);
            btnSablonYukle.TabIndex = 36;
            btnSablonYukle.Text = "📥 Salon Ataması İçin Dosya Yükle";
            btnSablonYukle.TextAlign = ContentAlignment.MiddleLeft;
            btnSablonYukle.UseVisualStyleBackColor = false;
            //
            // btnSalonExport
            //
            btnSalonExport.BackColor = Color.FromArgb(148, 226, 213);
            btnSalonExport.Cursor = Cursors.Hand;
            btnSalonExport.Enabled = false;
            btnSalonExport.FlatAppearance.BorderSize = 0;
            btnSalonExport.FlatStyle = FlatStyle.Flat;
            btnSalonExport.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSalonExport.ForeColor = Color.FromArgb(30, 30, 46);
            btnSalonExport.Location = new Point(545, 284);
            btnSalonExport.Name = "btnSalonExport";
            btnSalonExport.Size = new Size(250, 40);
            btnSalonExport.TabIndex = 38;
            btnSalonExport.Text = "📤 Salon Atanmış Dosya Çıkart";
            btnSalonExport.TextAlign = ContentAlignment.MiddleLeft;
            btnSalonExport.UseVisualStyleBackColor = false;
            //
            // btnGozetmenDosyaYukle
            //
            btnGozetmenDosyaYukle.BackColor = Color.FromArgb(203, 166, 247);
            btnGozetmenDosyaYukle.Cursor = Cursors.Hand;
            btnGozetmenDosyaYukle.FlatAppearance.BorderSize = 0;
            btnGozetmenDosyaYukle.FlatStyle = FlatStyle.Flat;
            btnGozetmenDosyaYukle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGozetmenDosyaYukle.ForeColor = Color.FromArgb(30, 30, 46);
            btnGozetmenDosyaYukle.Location = new Point(545, 348);
            btnGozetmenDosyaYukle.Name = "btnGozetmenDosyaYukle";
            btnGozetmenDosyaYukle.Size = new Size(250, 40);
            btnGozetmenDosyaYukle.TabIndex = 39;
            btnGozetmenDosyaYukle.Text = "📥 Gözetmenler için Dosya Yükle";
            btnGozetmenDosyaYukle.TextAlign = ContentAlignment.MiddleLeft;
            btnGozetmenDosyaYukle.UseVisualStyleBackColor = false;
            //
            // cmbSablonMod
            //
            cmbSablonMod.BackColor = Color.FromArgb(49, 50, 68);
            cmbSablonMod.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSablonMod.FlatStyle = FlatStyle.Flat;
            cmbSablonMod.Font = new Font("Segoe UI", 9F);
            cmbSablonMod.ForeColor = Color.FromArgb(205, 214, 244);
            cmbSablonMod.Items.AddRange(new object[] { "⚙ Otomatik Dağıt (Kurallı)", "📄 Boş Şablon" });
            cmbSablonMod.Location = new Point(326, 848);
            cmbSablonMod.Name = "cmbSablonMod";
            cmbSablonMod.Size = new Size(180, 23);
            cmbSablonMod.TabIndex = 35;
            //
            // btnSalonAta
            //
            btnSalonAta.BackColor = Color.FromArgb(137, 180, 250);
            btnSalonAta.Cursor = Cursors.Hand;
            btnSalonAta.Enabled = false;
            btnSalonAta.FlatAppearance.BorderSize = 0;
            btnSalonAta.FlatStyle = FlatStyle.Flat;
            btnSalonAta.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSalonAta.ForeColor = Color.FromArgb(30, 30, 46);
            btnSalonAta.Location = new Point(545, 238);
            btnSalonAta.Name = "btnSalonAta";
            btnSalonAta.Size = new Size(250, 40);
            btnSalonAta.TabIndex = 24;
            btnSalonAta.Text = "🚀 Salonları Ata";
            btnSalonAta.TextAlign = ContentAlignment.MiddleLeft;
            btnSalonAta.UseVisualStyleBackColor = false;
            //
            // btnTemizle
            //
            btnTemizle.BackColor = Color.FromArgb(88, 91, 112);
            btnTemizle.Cursor = Cursors.Hand;
            btnTemizle.FlatAppearance.BorderSize = 0;
            btnTemizle.FlatStyle = FlatStyle.Flat;
            btnTemizle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnTemizle.ForeColor = Color.FromArgb(205, 214, 244);
            btnTemizle.Location = new Point(16, 1128);
            btnTemizle.Name = "btnTemizle";
            btnTemizle.Size = new Size(790, 30);
            btnTemizle.TabIndex = 27;
            btnTemizle.Text = "🔄 Temizle";
            btnTemizle.UseVisualStyleBackColor = false;
            //
            // btnMatrisExport
            //
            btnMatrisExport.BackColor = Color.FromArgb(250, 179, 135);
            btnMatrisExport.Cursor = Cursors.Hand;
            btnMatrisExport.Enabled = false;
            btnMatrisExport.FlatAppearance.BorderSize = 0;
            btnMatrisExport.FlatStyle = FlatStyle.Flat;
            btnMatrisExport.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnMatrisExport.ForeColor = Color.FromArgb(30, 30, 46);
            btnMatrisExport.Location = new Point(545, 440);
            btnMatrisExport.Name = "btnMatrisExport";
            btnMatrisExport.Size = new Size(250, 40);
            btnMatrisExport.TabIndex = 25;
            btnMatrisExport.Text = "📊 Gözetmenli Taslak Çıkart";
            btnMatrisExport.UseVisualStyleBackColor = false;
            //
            // btnGozetmenliYukle — ADIM 7: elle düzenlenmiş gözetmenli dosyayı geri yükle
            //
            btnGozetmenliYukle.BackColor = Color.FromArgb(249, 226, 175);
            btnGozetmenliYukle.Cursor = Cursors.Hand;
            btnGozetmenliYukle.Enabled = false;
            btnGozetmenliYukle.FlatAppearance.BorderSize = 0;
            btnGozetmenliYukle.FlatStyle = FlatStyle.Flat;
            btnGozetmenliYukle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGozetmenliYukle.ForeColor = Color.FromArgb(30, 30, 46);
            btnGozetmenliYukle.Location = new Point(545, 486);
            btnGozetmenliYukle.Name = "btnGozetmenliYukle";
            btnGozetmenliYukle.Size = new Size(250, 40);
            btnGozetmenliYukle.TabIndex = 32;
            btnGozetmenliYukle.Text = "📥 Düzenlenmiş Dosyayı Yükle";
            btnGozetmenliYukle.UseVisualStyleBackColor = false;
            //
            // btnFinalExport — ADIM 7 sonu: güncel programın final çıktısı
            //
            btnFinalExport.BackColor = Color.FromArgb(166, 227, 161);
            btnFinalExport.Cursor = Cursors.Hand;
            btnFinalExport.Enabled = false;
            btnFinalExport.FlatAppearance.BorderSize = 0;
            btnFinalExport.FlatStyle = FlatStyle.Flat;
            btnFinalExport.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnFinalExport.ForeColor = Color.FromArgb(30, 30, 46);
            btnFinalExport.Location = new Point(545, 532);
            btnFinalExport.Name = "btnFinalExport";
            btnFinalExport.Size = new Size(250, 40);
            btnFinalExport.TabIndex = 33;
            btnFinalExport.Text = "🏁 Final Programı Çıkart";
            btnFinalExport.UseVisualStyleBackColor = false;
            //
            // btnVeritabaniSifirla
            //
            btnVeritabaniSifirla.BackColor = Color.FromArgb(243, 139, 168);
            btnVeritabaniSifirla.Cursor = Cursors.Hand;
            btnVeritabaniSifirla.FlatAppearance.BorderSize = 0;
            btnVeritabaniSifirla.FlatStyle = FlatStyle.Flat;
            btnVeritabaniSifirla.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnVeritabaniSifirla.ForeColor = Color.FromArgb(30, 30, 46);
            btnVeritabaniSifirla.Location = new Point(16, 1090);
            btnVeritabaniSifirla.Name = "btnVeritabaniSifirla";
            btnVeritabaniSifirla.Size = new Size(790, 34);
            btnVeritabaniSifirla.TabIndex = 26;
            btnVeritabaniSifirla.Text = "🗑 Uygulamayı Sıfırla (Fabrika Ayarlarına Dön)";
            btnVeritabaniSifirla.UseVisualStyleBackColor = false;
            //
            // lblDbDurum
            //
            lblDbDurum.AutoSize = true;
            lblDbDurum.Font = new Font("Segoe UI", 9F);
            lblDbDurum.Location = new Point(16, 1066);
            lblDbDurum.Name = "lblDbDurum";
            lblDbDurum.Size = new Size(212, 15);
            lblDbDurum.TabIndex = 27;
            lblDbDurum.Text = "⏳ Veritabanı durumu kontrol ediliyor...";
            lblDbDurum.Click += lblDbDurum_Click;
            //
            // lblGozetmenBaslik
            //
            lblGozetmenBaslik.AutoSize = true;
            lblGozetmenBaslik.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblGozetmenBaslik.ForeColor = Color.FromArgb(198, 160, 246);
            lblGozetmenBaslik.Location = new Point(16, 838);
            lblGozetmenBaslik.Name = "lblGozetmenBaslik";
            lblGozetmenBaslik.Size = new Size(400, 25);
            lblGozetmenBaslik.TabIndex = 31;
            lblGozetmenBaslik.Text = "🧑‍🏫 Gözetmenler Slotu (Arş. Gör.)";
            //
            // btnGozetmenYukle
            //
            btnGozetmenYukle.BackColor = Color.FromArgb(249, 226, 175);
            btnGozetmenYukle.Cursor = Cursors.Hand;
            btnGozetmenYukle.FlatAppearance.BorderSize = 0;
            btnGozetmenYukle.FlatStyle = FlatStyle.Flat;
            btnGozetmenYukle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGozetmenYukle.ForeColor = Color.FromArgb(30, 30, 46);
            btnGozetmenYukle.Location = new Point(16, 868);
            btnGozetmenYukle.Name = "btnGozetmenYukle";
            btnGozetmenYukle.Size = new Size(300, 34);
            btnGozetmenYukle.TabIndex = 32;
            btnGozetmenYukle.Text = "📂 Gözetmen Yükle (Excel)";
            btnGozetmenYukle.UseVisualStyleBackColor = false;
            //
            // btnGozetmenAta
            //
            btnGozetmenAta.BackColor = Color.FromArgb(198, 160, 246);
            btnGozetmenAta.Cursor = Cursors.Hand;
            btnGozetmenAta.Enabled = false;
            btnGozetmenAta.FlatAppearance.BorderSize = 0;
            btnGozetmenAta.FlatStyle = FlatStyle.Flat;
            btnGozetmenAta.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnGozetmenAta.ForeColor = Color.FromArgb(30, 30, 46);
            btnGozetmenAta.Location = new Point(545, 394);
            btnGozetmenAta.Name = "btnGozetmenAta";
            btnGozetmenAta.Size = new Size(250, 34);
            btnGozetmenAta.TabIndex = 33;
            btnGozetmenAta.Text = "🎯 Gözetmen Ata";
            btnGozetmenAta.UseVisualStyleBackColor = false;
            //
            // dgvGozetmenler
            //
            dgvGozetmenler.AllowUserToAddRows = false;
            dgvGozetmenler.AllowUserToDeleteRows = false;
            dgvGozetmenler.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvGozetmenler.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvGozetmenler.BorderStyle = BorderStyle.None;
            dgvGozetmenler.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dgvGozetmenler.DefaultCellStyle = dataGridViewCellStyle4;
            dgvGozetmenler.EnableHeadersVisualStyles = false;
            dgvGozetmenler.GridColor = Color.FromArgb(69, 71, 90);
            dgvGozetmenler.Location = new Point(16, 906);
            dgvGozetmenler.Name = "dgvGozetmenler";
            dgvGozetmenler.RowHeadersVisible = false;
            dgvGozetmenler.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvGozetmenler.Size = new Size(490, 150);
            dgvGozetmenler.TabIndex = 34;
            //
            // panelSag
            //
            panelSag.BackColor = Color.FromArgb(30, 30, 46);
            panelSag.Controls.Add(lblSonucBaslik);
            panelSag.Controls.Add(lblDurum);
            panelSag.Controls.Add(tabControlSonuc);
            panelSag.Dock = DockStyle.Fill;
            panelSag.Location = new Point(530, 0);
            panelSag.Name = "panelSag";
            panelSag.Padding = new Padding(16);
            panelSag.Size = new Size(1020, 980);
            panelSag.TabIndex = 0;
            // 
            // lblSonucBaslik
            // 
            lblSonucBaslik.AutoSize = true;
            lblSonucBaslik.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblSonucBaslik.ForeColor = Color.FromArgb(166, 227, 161);
            lblSonucBaslik.Location = new Point(16, 8);
            lblSonucBaslik.Name = "lblSonucBaslik";
            lblSonucBaslik.Size = new Size(265, 25);
            lblSonucBaslik.TabIndex = 0;
            lblSonucBaslik.Text = "📋 Sınav Programı Sonuçları";
            // 
            // lblDurum
            // 
            lblDurum.AutoSize = true;
            lblDurum.Font = new Font("Segoe UI", 10F, FontStyle.Italic);
            lblDurum.ForeColor = Color.FromArgb(166, 173, 200);
            lblDurum.Location = new Point(16, 42);
            lblDurum.Name = "lblDurum";
            lblDurum.Size = new Size(201, 19);
            lblDurum.TabIndex = 1;
            lblDurum.Text = "Henüz program oluşturulmadı.";
            // 
            // tabControlSonuc
            // 
            tabControlSonuc.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControlSonuc.Controls.Add(tabSablon);
            tabControlSonuc.Controls.Add(tabSonuclar);
            tabControlSonuc.Controls.Add(tabMatris);
            tabControlSonuc.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            tabControlSonuc.Location = new Point(19, 80);
            tabControlSonuc.Name = "tabControlSonuc";
            tabControlSonuc.SelectedIndex = 0;
            tabControlSonuc.Size = new Size(982, 881);
            tabControlSonuc.TabIndex = 2;
            //
            // tabSablon
            //
            tabSablon.BackColor = Color.FromArgb(30, 30, 46);
            tabSablon.Controls.Add(dgvSablon);
            tabSablon.Location = new Point(4, 26);
            tabSablon.Name = "tabSablon";
            tabSablon.Padding = new Padding(8);
            tabSablon.Size = new Size(974, 851);
            tabSablon.TabIndex = 2;
            tabSablon.Text = "📐 Şablon Görünümü";
            //
            // dgvSablon
            //
            dgvSablon.AllowUserToAddRows = false;
            dgvSablon.AllowUserToDeleteRows = false;
            dgvSablon.AllowUserToResizeRows = false;
            dgvSablon.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvSablon.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle9.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle9.BackColor = Color.FromArgb(69, 71, 90);
            dataGridViewCellStyle9.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dataGridViewCellStyle9.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle9.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle9.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle9.WrapMode = DataGridViewTriState.True;
            dgvSablon.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle9;
            dgvSablon.ColumnHeadersHeight = 48;
            dgvSablon.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dataGridViewCellStyle10.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle10.BackColor = Color.FromArgb(49, 50, 68);
            dataGridViewCellStyle10.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle10.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle10.Padding = new Padding(2);
            dataGridViewCellStyle10.SelectionBackColor = Color.FromArgb(137, 180, 250);
            dataGridViewCellStyle10.SelectionForeColor = Color.FromArgb(30, 30, 46);
            dataGridViewCellStyle10.WrapMode = DataGridViewTriState.False;
            dgvSablon.DefaultCellStyle = dataGridViewCellStyle10;
            dgvSablon.Dock = DockStyle.Fill;
            dgvSablon.EnableHeadersVisualStyles = false;
            dgvSablon.GridColor = Color.FromArgb(69, 71, 90);
            dgvSablon.Location = new Point(8, 8);
            dgvSablon.Name = "dgvSablon";
            dgvSablon.ReadOnly = false;
            dgvSablon.RowHeadersVisible = false;
            dgvSablon.RowTemplate.Height = 26;
            dgvSablon.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvSablon.Size = new Size(958, 835);
            dgvSablon.TabIndex = 0;
            //
            // tabSonuclar
            //
            tabSonuclar.BackColor = Color.FromArgb(30, 30, 46);
            tabSonuclar.Controls.Add(dgvSonuc);
            tabSonuclar.Location = new Point(4, 26);
            tabSonuclar.Name = "tabSonuclar";
            tabSonuclar.Padding = new Padding(8);
            tabSonuclar.Size = new Size(974, 851);
            tabSonuclar.TabIndex = 0;
            tabSonuclar.Text = "📋 Sınav Sonuçları";
            // 
            // dgvSonuc
            // 
            dgvSonuc.AllowUserToAddRows = false;
            dgvSonuc.AllowUserToDeleteRows = false;
            dgvSonuc.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvSonuc.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvSonuc.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = Color.FromArgb(69, 71, 90);
            dataGridViewCellStyle5.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewCellStyle5.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle5.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = DataGridViewTriState.True;
            dgvSonuc.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            dgvSonuc.ColumnHeadersHeight = 40;
            dataGridViewCellStyle6.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = Color.FromArgb(49, 50, 68);
            dataGridViewCellStyle6.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewCellStyle6.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle6.Padding = new Padding(4);
            dataGridViewCellStyle6.SelectionBackColor = Color.FromArgb(137, 180, 250);
            dataGridViewCellStyle6.SelectionForeColor = Color.FromArgb(30, 30, 46);
            dataGridViewCellStyle6.WrapMode = DataGridViewTriState.False;
            dgvSonuc.DefaultCellStyle = dataGridViewCellStyle6;
            dgvSonuc.Dock = DockStyle.Fill;
            dgvSonuc.EnableHeadersVisualStyles = false;
            dgvSonuc.GridColor = Color.FromArgb(69, 71, 90);
            dgvSonuc.Location = new Point(8, 8);
            dgvSonuc.Name = "dgvSonuc";
            dgvSonuc.ReadOnly = true;
            dgvSonuc.RowHeadersVisible = false;
            dgvSonuc.RowTemplate.Height = 35;
            dgvSonuc.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvSonuc.Size = new Size(958, 835);
            dgvSonuc.TabIndex = 0;
            // 
            // tabMatris
            // 
            tabMatris.BackColor = Color.FromArgb(30, 30, 46);
            tabMatris.Controls.Add(dgvMatris);
            tabMatris.Controls.Add(panelMatrisFiltre);
            tabMatris.Location = new Point(4, 26);
            tabMatris.Name = "tabMatris";
            tabMatris.Padding = new Padding(8);
            tabMatris.Size = new Size(937, 726);
            tabMatris.TabIndex = 1;
            tabMatris.Text = "📊 Matris Görünüm";
            // 
            // dgvMatris
            // 
            dgvMatris.AllowUserToAddRows = false;
            dgvMatris.AllowUserToDeleteRows = false;
            dgvMatris.BackgroundColor = Color.FromArgb(49, 50, 68);
            dgvMatris.BorderStyle = BorderStyle.None;
            dataGridViewCellStyle7.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = Color.FromArgb(69, 71, 90);
            dataGridViewCellStyle7.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewCellStyle7.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle7.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle7.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = DataGridViewTriState.True;
            dgvMatris.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            dgvMatris.ColumnHeadersHeight = 40;
            dataGridViewCellStyle8.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = Color.FromArgb(49, 50, 68);
            dataGridViewCellStyle8.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewCellStyle8.ForeColor = Color.FromArgb(205, 214, 244);
            dataGridViewCellStyle8.Padding = new Padding(2);
            dataGridViewCellStyle8.SelectionBackColor = Color.FromArgb(137, 180, 250);
            dataGridViewCellStyle8.SelectionForeColor = Color.FromArgb(30, 30, 46);
            dataGridViewCellStyle8.WrapMode = DataGridViewTriState.False;
            dgvMatris.DefaultCellStyle = dataGridViewCellStyle8;
            dgvMatris.Dock = DockStyle.Fill;
            dgvMatris.EnableHeadersVisualStyles = false;
            dgvMatris.GridColor = Color.FromArgb(69, 71, 90);
            dgvMatris.Location = new Point(8, 54);
            dgvMatris.Name = "dgvMatris";
            dgvMatris.ReadOnly = false;
            dgvMatris.RowHeadersVisible = false;
            dgvMatris.RowTemplate.Height = 30;
            dgvMatris.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgvMatris.Size = new Size(921, 664);
            dgvMatris.TabIndex = 0;
            // 
            // panelMatrisFiltre
            // 
            panelMatrisFiltre.BackColor = Color.FromArgb(36, 36, 58);
            panelMatrisFiltre.Controls.Add(lblFiltreBolum);
            panelMatrisFiltre.Controls.Add(cmbFiltreBolum);
            panelMatrisFiltre.Controls.Add(lblFiltreSinif);
            panelMatrisFiltre.Controls.Add(cmbFiltreSinif);
            panelMatrisFiltre.Dock = DockStyle.Top;
            panelMatrisFiltre.Location = new Point(8, 8);
            panelMatrisFiltre.Name = "panelMatrisFiltre";
            panelMatrisFiltre.Padding = new Padding(4);
            panelMatrisFiltre.Size = new Size(921, 46);
            panelMatrisFiltre.TabIndex = 1;
            // 
            // lblFiltreBolum
            // 
            lblFiltreBolum.AutoSize = true;
            lblFiltreBolum.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblFiltreBolum.ForeColor = Color.FromArgb(205, 214, 244);
            lblFiltreBolum.Location = new Point(8, 12);
            lblFiltreBolum.Name = "lblFiltreBolum";
            lblFiltreBolum.Size = new Size(46, 15);
            lblFiltreBolum.TabIndex = 0;
            lblFiltreBolum.Text = "Bölüm:";
            // 
            // cmbFiltreBolum
            // 
            cmbFiltreBolum.BackColor = Color.FromArgb(49, 50, 68);
            cmbFiltreBolum.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFiltreBolum.FlatStyle = FlatStyle.Flat;
            cmbFiltreBolum.Font = new Font("Segoe UI", 9F);
            cmbFiltreBolum.ForeColor = Color.FromArgb(205, 214, 244);
            cmbFiltreBolum.Location = new Point(66, 8);
            cmbFiltreBolum.Name = "cmbFiltreBolum";
            cmbFiltreBolum.Size = new Size(300, 23);
            cmbFiltreBolum.TabIndex = 1;
            // 
            // lblFiltreSinif
            // 
            lblFiltreSinif.AutoSize = true;
            lblFiltreSinif.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblFiltreSinif.ForeColor = Color.FromArgb(205, 214, 244);
            lblFiltreSinif.Location = new Point(380, 12);
            lblFiltreSinif.Name = "lblFiltreSinif";
            lblFiltreSinif.Size = new Size(35, 15);
            lblFiltreSinif.TabIndex = 2;
            lblFiltreSinif.Text = "Sınıf:";
            // 
            // cmbFiltreSinif
            // 
            cmbFiltreSinif.BackColor = Color.FromArgb(49, 50, 68);
            cmbFiltreSinif.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFiltreSinif.FlatStyle = FlatStyle.Flat;
            cmbFiltreSinif.Font = new Font("Segoe UI", 9F);
            cmbFiltreSinif.ForeColor = Color.FromArgb(205, 214, 244);
            cmbFiltreSinif.Location = new Point(430, 8);
            cmbFiltreSinif.Name = "cmbFiltreSinif";
            cmbFiltreSinif.Size = new Size(100, 23);
            cmbFiltreSinif.TabIndex = 3;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(30, 30, 46);
            ClientSize = new Size(852, 632);
            Controls.Add(panelSag);
            Controls.Add(panelSol);
            Font = new Font("Segoe UI", 10F);
            ForeColor = Color.FromArgb(205, 214, 244);
            // Pencere sabit boyutlu: büyütme/yeniden boyutlandırma kapalı.
            // (İçerik mutlak konumlu olduğu için büyütmek boş alan üretiyordu;
            //  sığmayan içerik sol paneldeki kaydırma çubuğuyla gezilir.)
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimumSize = new Size(720, 560);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Üniversite Sınav Programı Oluşturucu V6";
            Load += MainForm_Load;
            panelSol.ResumeLayout(false);
            panelSol.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numSinifSeviyesi).EndInit();
            ((System.ComponentModel.ISupportInitialize)numOgrenciSayisi).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvDersler).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvOverbooking).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvHataOrani).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvGozetmenler).EndInit();
            panelSag.ResumeLayout(false);
            panelSag.PerformLayout();
            tabControlSonuc.ResumeLayout(false);
            tabSablon.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvSablon).EndInit();
            tabSonuclar.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvSonuc).EndInit();
            tabMatris.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvMatris).EndInit();
            panelMatrisFiltre.ResumeLayout(false);
            panelMatrisFiltre.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        // Sol Panel
        private System.Windows.Forms.Panel panelSol;
        private System.Windows.Forms.Label lblDersBaslik;
        private System.Windows.Forms.Label lblSubeKuraliUyari;
        private System.Windows.Forms.Label lblBolum;
        private System.Windows.Forms.CheckedListBox clbBolum;
        private System.Windows.Forms.Button btnHavuzYonetimi;
        private System.Windows.Forms.TextBox txtDersAra;
        private System.Windows.Forms.Label lblSinifSeviyesi;
        private System.Windows.Forms.NumericUpDown numSinifSeviyesi;
        private System.Windows.Forms.Label lblDersAdi;
        private System.Windows.Forms.ComboBox cmbDersAdi;
        private System.Windows.Forms.Label lblSube;
        private System.Windows.Forms.TextBox txtSube;
        private System.Windows.Forms.Label lblOgrenciSayisi;
        private System.Windows.Forms.NumericUpDown numOgrenciSayisi;
        private System.Windows.Forms.Button btnDersEkle;
        private System.Windows.Forms.Button btnDersSil;
        private System.Windows.Forms.Button btnExcelImport;

        // Overbooking
        private System.Windows.Forms.Label lblOverbooking;
        private System.Windows.Forms.TextBox txtUygulamaliAra;
        private System.Windows.Forms.DataGridView dgvOverbooking;
        private System.Windows.Forms.Label lblHataOrani;
        private System.Windows.Forms.DataGridView dgvHataOrani;

        // Zaman Ayarı
        private System.Windows.Forms.Label lblZamanBaslik;
        private System.Windows.Forms.MonthCalendar calendarSinav;
        private System.Windows.Forms.CheckBox chkGunEtkin;
        private System.Windows.Forms.FlowLayoutPanel flowSaatler;

        // Aksiyon Butonları — Aşamalı Akış
        private System.Windows.Forms.Button btnSablonOlustur;   // ADIM 2: Çekilmiş Dersler Excel'i üret (boş şablon)
        private System.Windows.Forms.Button btnOtomatikDagit;   // Şablon Oluştur (Otomatik Dağılmış)
        private System.Windows.Forms.Button btnSablonYukle;     // Salon Ataması İçin Dosya Yükle
        private System.Windows.Forms.Button btnSalonExport;     // Salon Atanmış Dosya Çıkart
        private System.Windows.Forms.Button btnGozetmenDosyaYukle; // Gözetmenler için Dosya Yükle
        private System.Windows.Forms.ComboBox cmbSablonMod;     // Otomatik Dağıt (Kurallı) / Boş Şablon seçimi
        private System.Windows.Forms.Button btnSalonAta;        // AŞAMA C: Toptan salon dağıtımı
        private System.Windows.Forms.Button btnTemizle;
        private System.Windows.Forms.Button btnMatrisExport;
        private System.Windows.Forms.Button btnGozetmenliYukle;
        private System.Windows.Forms.Button btnFinalExport;
        private System.Windows.Forms.Button btnVeritabaniSifirla;
        private System.Windows.Forms.Label lblDbDurum;

        // Gözetmen Atama Modülü
        private System.Windows.Forms.Label lblGozetmenBaslik;
        private System.Windows.Forms.Button btnGozetmenYukle;
        private System.Windows.Forms.Button btnGozetmenAta;
        private System.Windows.Forms.DataGridView dgvGozetmenler;

        // Sağ Panel — TabControl
        private System.Windows.Forms.Panel panelSag;
        private System.Windows.Forms.Label lblSonucBaslik;
        private System.Windows.Forms.Label lblDurum;
        private System.Windows.Forms.TabControl tabControlSonuc;
        private System.Windows.Forms.TabPage tabSablon;
        private System.Windows.Forms.TabPage tabSonuclar;
        private System.Windows.Forms.TabPage tabMatris;
        private System.Windows.Forms.DataGridView dgvSablon;
        private System.Windows.Forms.DataGridView dgvSonuc;
        private System.Windows.Forms.DataGridView dgvMatris;
        private System.Windows.Forms.Panel panelMatrisFiltre;
        private System.Windows.Forms.ComboBox cmbFiltreBolum;
        private System.Windows.Forms.ComboBox cmbFiltreSinif;

        // dgvDersler
        private System.Windows.Forms.DataGridView dgvDersler;
        private Label separator;
        private Label lblFiltreBolum;
        private Label lblFiltreSinif;
    }
}
