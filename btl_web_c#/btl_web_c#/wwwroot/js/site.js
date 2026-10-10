// site.js – shared JS helpers

// ── Toast helper ────────────────────────────────────────────────
window.showToast = function (message, type = 'success', duration = 4000) {
    const container = document.getElementById('toast-container');
    if (!container) return;

    const id = 'toast-' + Date.now();
    const iconMap = { success: 'bi-check-circle-fill', danger: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
    const icon = iconMap[type] || 'bi-info-circle-fill';

    const html = `
      <div id="${id}" class="toast align-items-center text-bg-${type} border-0 show" role="alert" aria-live="assertive">
        <div class="d-flex">
          <div class="toast-body"><i class="bi ${icon} me-2"></i>${message}</div>
          <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
      </div>`;
    container.insertAdjacentHTML('beforeend', html);

    if (duration > 0) {
        setTimeout(() => {
            const el = document.getElementById(id);
            if (el) el.remove();
        }, duration);
    }
};

// ── Confirm modal helper ────────────────────────────────────────
window.confirmAction = function (message, callback) {
    const modal = document.getElementById('confirm-modal');
    if (!modal) { if (confirm(message)) callback(); return; }

    document.getElementById('confirm-modal-body').textContent = message;
    const bsModal = new bootstrap.Modal(modal);
    bsModal.show();

    const btn = document.getElementById('confirm-modal-ok');
    const handler = () => { bsModal.hide(); btn.removeEventListener('click', handler); callback(); };
    btn.addEventListener('click', handler);
};

// ── Sidebar toggle (Admin, mobile) ─────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    const toggleBtn = document.getElementById('sidebar-toggle');
    const sidebar = document.querySelector('.admin-sidebar');
    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => sidebar.classList.toggle('open'));
    }

    // Auto-dismiss alerts after 5s
    document.querySelectorAll('.alert-auto-dismiss').forEach(el => {
        setTimeout(() => el.remove(), 5000);
    });
});
