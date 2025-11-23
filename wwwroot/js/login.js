// UI-only client validation and interactions for the login page

function togglePassword(button) {
  const input = document.getElementById('password');
  const show = input.type === 'password';
  input.type = show ? 'text' : 'password';
  if (button) button.textContent = show ? 'Hide' : 'Show';
  input.focus();
}

function showToast(message, timeout = 3000) {
  const t = document.getElementById('toast');
  if (!t) return;
  t.textContent = message;
  t.style.opacity = '1';
  t.style.transform = 'translateY(0)';
  clearTimeout(showToast._timer);
  showToast._timer = setTimeout(() => {
    t.style.opacity = '0';
    t.style.transform = 'translateY(12px)';
  }, timeout);
}

function validateEmail(value) {
  return /\S+@\S+\.\S+/.test(value);
}

function validateForm(data) {
  const errors = {};
  if (!data.email || !validateEmail(data.email)) errors.email = 'Please enter a valid email.';
  if (!data.password || data.password.length < 8) errors.password = 'Password must be at least 8 characters.';
  return errors;
}

function clearErrors() {
  document.getElementById('emailError').textContent = '';
  document.getElementById('passwordError').textContent = '';
}

function handleLogin(evt) {
  evt.preventDefault();
  clearErrors();
  const email = document.getElementById('email').value.trim();
  const password = document.getElementById('password').value;
  const remember = document.getElementById('remember').checked;
  const data = { email, password, remember };

  const errors = validateForm(data);
  if (errors.email) document.getElementById('emailError').textContent = errors.email;
  if (errors.password) document.getElementById('passwordError').textContent = errors.password;
  if (Object.keys(errors).length) return;

  const submitBtn = document.getElementById('submitBtn');
  submitBtn.disabled = true;
  const originalText = submitBtn.textContent;
  submitBtn.textContent = 'Signing in...';

  setTimeout(() => {
    submitBtn.disabled = false;
    submitBtn.textContent = originalText;

    if (email.toLowerCase().includes('user')) {
      showToast('Signed in — UI only demo');
    } else {
      showToast('Invalid credentials (UI demo)');
      document.getElementById('passwordError').textContent = 'Invalid email or password.';
    }
  }, 1000 + Math.random() * 800);
}

function socialStub(provider) {
  showToast(provider + ' sign-in (UI demo)');
}

document.addEventListener('DOMContentLoaded', () => {
  const email = document.getElementById('email');
  if (email) email.focus();
});