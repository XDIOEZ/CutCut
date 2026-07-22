(() => {
  const carousel = document.querySelector("[data-showcase-carousel]");
  if (!carousel) {
    return;
  }

  const slides = Array.from(carousel.querySelectorAll("[data-showcase-slide]"));
  const dots = Array.from(carousel.querySelectorAll("[data-showcase-dot]"));
  const previousButton = carousel.querySelector("[data-showcase-previous]");
  const nextButton = carousel.querySelector("[data-showcase-next]");
  const autoplayButton = carousel.querySelector("[data-showcase-autoplay]");
  const status = carousel.querySelector("[data-showcase-status]");
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const intervalMilliseconds = 6000;

  let activeIndex = 0;
  let timerId = null;
  let autoplayEnabled = !reducedMotion.matches;
  let temporarilyPaused = false;

  function normalizeIndex(index) {
    return (index + slides.length) % slides.length;
  }

  function render(index, announce = true) {
    activeIndex = normalizeIndex(index);

    slides.forEach((slide, slideIndex) => {
      const active = slideIndex === activeIndex;
      slide.classList.toggle("is-active", active);
      slide.setAttribute("aria-hidden", String(!active));
      slide.querySelectorAll("a, button").forEach((control) => {
        control.tabIndex = active ? 0 : -1;
      });
    });

    dots.forEach((dot, dotIndex) => {
      const active = dotIndex === activeIndex;
      dot.classList.toggle("is-active", active);
      if (active) {
        dot.setAttribute("aria-current", "true");
      } else {
        dot.removeAttribute("aria-current");
      }
    });

    if (announce) {
      status.textContent = `正在显示第 ${activeIndex + 1} 张，共 ${slides.length} 张。`;
    }
  }

  function stopTimer() {
    window.clearTimeout(timerId);
    timerId = null;
  }

  function scheduleNext() {
    stopTimer();
    if (!autoplayEnabled || temporarilyPaused || document.hidden) {
      return;
    }

    timerId = window.setTimeout(() => {
      render(activeIndex + 1, false);
      scheduleNext();
    }, intervalMilliseconds);
  }

  function updateAutoplayButton() {
    carousel.dataset.playing = String(autoplayEnabled);
    autoplayButton.textContent = autoplayEnabled ? "暂停自动播放" : "开始自动播放";
    autoplayButton.setAttribute(
      "aria-label",
      autoplayEnabled ? "暂停宣传图自动播放" : "开始宣传图自动播放",
    );
  }

  function showSlide(index) {
    render(index);
    scheduleNext();
  }

  previousButton.addEventListener("click", () => showSlide(activeIndex - 1));
  nextButton.addEventListener("click", () => showSlide(activeIndex + 1));

  dots.forEach((dot) => {
    dot.addEventListener("click", () => showSlide(Number(dot.dataset.showcaseDot)));
  });

  autoplayButton.addEventListener("click", () => {
    autoplayEnabled = !autoplayEnabled;
    updateAutoplayButton();
    scheduleNext();
  });

  carousel.addEventListener("keydown", (event) => {
    if (event.key === "ArrowLeft") {
      event.preventDefault();
      showSlide(activeIndex - 1);
    } else if (event.key === "ArrowRight") {
      event.preventDefault();
      showSlide(activeIndex + 1);
    } else if (event.key === "Home") {
      event.preventDefault();
      showSlide(0);
    } else if (event.key === "End") {
      event.preventDefault();
      showSlide(slides.length - 1);
    }
  });

  carousel.addEventListener("mouseenter", () => {
    temporarilyPaused = true;
    stopTimer();
  });

  carousel.addEventListener("mouseleave", () => {
    temporarilyPaused = false;
    scheduleNext();
  });

  carousel.addEventListener("focusin", () => {
    temporarilyPaused = true;
    stopTimer();
  });

  carousel.addEventListener("focusout", (event) => {
    if (carousel.contains(event.relatedTarget)) {
      return;
    }

    temporarilyPaused = false;
    scheduleNext();
  });

  document.addEventListener("visibilitychange", scheduleNext);
  reducedMotion.addEventListener("change", (event) => {
    if (event.matches) {
      autoplayEnabled = false;
      stopTimer();
      updateAutoplayButton();
    }
  });

  render(0, false);
  updateAutoplayButton();
  scheduleNext();
})();
