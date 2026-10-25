// Sınav Programı — arayüzün küçük tarayıcı yardımcıları (C# tarafından JS interop ile çağrılır).
window.sinavProgrami = {
    // Metni panoya kopyalar (uyarı listeleri vb.)
    panoyaKopyala: function (metin) {
        return navigator.clipboard.writeText(metin);
    },

    // Hücre düzenleyicisi açılınca kutuya odaklanıp içindeki metni seçer (Excel'deki gibi üzerine yazılır)
    secVeOdakla: function (oge) {
        if (!oge) return;
        oge.focus();
        if (oge.select) oge.select();
    },

    // Bir öğeyi görünür alana kaydırır (ör. ihlal listesinden hücreye gitme)
    gorunureKaydir: function (secici) {
        var oge = document.querySelector(secici);
        if (oge) oge.scrollIntoView({ block: "center", inline: "center", behavior: "smooth" });
    },

    // Kutuya odaklanır, imleci metnin sonuna koyar (hücrede yazmaya başlanarak açılan düzenleyici)
    sonaOdakla: function (oge) {
        if (!oge) return;
        oge.focus();
        var n = (oge.value || "").length;
        if (oge.setSelectionRange) oge.setSelectionRange(n, n);
    },

    // Açılır listeyi kutunun altına (yer yoksa üstüne) sabit konumla yerleştirir; vurgulu öğeyi görünür tutar.
    // Sabit konum, listenin kaydırılabilir tablo kabına kırpılmasını önler.
    altinaYerlestir: function (kutu, liste, vurgu) {
        if (!kutu || !liste) return;
        var r = kutu.getBoundingClientRect();
        var yukseklik = Math.min(liste.scrollHeight, 320);
        var altta = window.innerHeight - r.bottom - 8;
        liste.style.left = Math.max(8, Math.min(r.left, window.innerWidth - Math.max(r.width, 340) - 8)) + "px";
        liste.style.minWidth = Math.max(r.width, 340) + "px";
        if (altta >= Math.min(yukseklik, 200) || altta >= r.top) {
            liste.style.top = (r.bottom + 2) + "px";
            liste.style.bottom = "";
            liste.style.maxHeight = Math.max(120, Math.min(320, altta)) + "px";
        } else {
            liste.style.top = "";
            liste.style.bottom = (window.innerHeight - r.top + 2) + "px";
            liste.style.maxHeight = Math.max(120, Math.min(320, r.top - 8)) + "px";
        }
        if (vurgu >= 0 && liste.children[vurgu]) liste.children[vurgu].scrollIntoView({ block: "nearest" });
    },

    // Excel gibi ızgaralar (şablon, salon matrisi): fare ve klavye olayları tablo düzeyinde toplanıp
    // .NET'e iletilir (binlerce hücreye ayrı ayrı olay bağlamamak için). Hücreler td[data-r][data-c].
    izgara: {
        bagla: function (tablo, dotnet) {
            if (!tablo || tablo._izgaraBagli) return;
            tablo._izgaraBagli = true;
            var hucre = function (e) {
                if (e.target.closest("input, .ders-secici")) return null;
                return e.target.closest("td[data-r]");
            };
            var GEZINME = ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Home", "End",
                           "PageUp", "PageDown", "Enter", "F2", "Delete", "Backspace"];

            tablo.addEventListener("mousedown", function (e) {
                var h = hucre(e);
                if (!h || e.button !== 0) return;
                if (e.shiftKey) e.preventDefault();   // Shift ile seçimde metin seçilmesin
                dotnet.invokeMethodAsync("Tikla", +h.dataset.r, +h.dataset.c, e.ctrlKey || e.metaKey, e.shiftKey);
            });
            tablo.addEventListener("dblclick", function (e) {
                var h = hucre(e);
                if (!h) return;
                e.preventDefault();
                dotnet.invokeMethodAsync("CiftTikla", +h.dataset.r, +h.dataset.c);
            });
            tablo.addEventListener("keydown", function (e) {
                var h = hucre(e);
                if (!h || e.altKey) return;
                var yazi = e.key.length === 1 && !e.ctrlKey && !e.metaKey;
                if (GEZINME.indexOf(e.key) < 0 && !yazi) return;
                e.preventDefault();
                dotnet.invokeMethodAsync("Tus", +h.dataset.r, +h.dataset.c, e.key, e.ctrlKey || e.metaKey, e.shiftKey);
            });
            tablo.parentElement.addEventListener("wheel", function (e) {
                if (!e.ctrlKey) return;
                e.preventDefault();
                dotnet.invokeMethodAsync("Tekerlek", e.deltaY < 0 ? 1 : -1);
            }, { passive: false });
        },

        // Klavyeyle gezinmede etkin hücreye odaklanır (yapışkan başlık / sabit sütunlar altında kalmasın).
        // Sanal kaydırmalı tabloda satır henüz çizilmemişse önce kap o satıra kaydırılır, hücre gelince odaklanılır.
        odakla: function (id, kapId, satir, satirYuksekligi) {
            var o = document.getElementById(id);
            if (o) {
                o.focus({ preventScroll: true });
                o.scrollIntoView({ block: "nearest", inline: "nearest" });
                return;
            }
            var kap = kapId && document.getElementById(kapId);
            if (!kap || !(satirYuksekligi > 0)) return;
            kap.scrollTop = Math.max(0, satir * satirYuksekligi - kap.clientHeight / 2);
            var deneme = 0;
            (function bekle() {
                var h = document.getElementById(id);
                if (h) { h.focus({ preventScroll: true }); h.scrollIntoView({ block: "nearest", inline: "nearest" }); }
                else if (++deneme < 30) requestAnimationFrame(bekle);
            })();
        },

        // İhlal listesinden gelen "hücreye git": ortalar, odaklanır ve kısa süre vurgular
        gozeGetir: function (id) {
            var o = document.getElementById(id);
            if (!o) return;
            o.scrollIntoView({ block: "center", inline: "center", behavior: "smooth" });
            o.focus({ preventScroll: true });
            o.classList.remove("parla");
            void o.offsetWidth;
            o.classList.add("parla");
        },

        // Satır içi düzenleyici açılınca kutuya odaklanır: yazmaya başlanarak açıldıysa imleç sonda, değilse metin seçili
        kutuyaOdaklan: function (id) {
            var td = document.getElementById(id);
            var kutu = td && td.querySelector("input");
            if (!kutu) return;
            if (kutu.dataset.ilk === "son") window.sinavProgrami.sonaOdakla(kutu);
            else window.sinavProgrami.secVeOdakla(kutu);
        }
    }
};
