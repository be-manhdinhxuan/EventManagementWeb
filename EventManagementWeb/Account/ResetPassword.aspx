<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ResetPassword.aspx.cs" Inherits="EventManagementWeb.Account.ResetPassword" %>

<!DOCTYPE html>
<html lang="vi">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Đặt lại mật khẩu</title>
    <link href="../Assets/css/base/reset.css" rel="stylesheet" />
    <link href="../Assets/css/style.css" rel="stylesheet" />
    <link href="../Assets/css/pages/reset_pass.css" rel="stylesheet" />

</head>
<body>
    <form id="form1" runat="server">
        <div class="reset-box">
            <h2 class="title">Đặt lại mật khẩu</h2>
            <p class="desc">Tạo một mật khẩu mới, mạnh cho tài khoản của bạn</p>

            <!-- Mật khẩu mới -->
            <div class="form-group">
                <label>Mật khẩu mới</label>
                <div class="password-wrapper">
                    <input type="password" name="newPassword" id="newPassword" 
                           class="password-input" placeholder="Nhập mật khẩu mới" 
                           onkeyup="checkPasswordStrength()" required />
                    <img src="../Assets/images/icon/eye-close.svg"
                        id="toggleIcon"
                        class="toggle-password"
                        onclick="togglePasswordVisibility()"
                        alt="Hiện/ẩn mật khẩu" />
                </div>

                <!-- Thanh độ mạnh -->
                <div class="strength-text" id="strengthText">Độ mạnh của mật khẩu</div>
                <div class="strength-meter">
                    <div class="strength-bar" id="strengthBar"></div>
                </div>
            </div>

            <!-- Xác nhận mật khẩu -->
            <div class="form-group">
                <label>Xác nhận mật khẩu mới</label>
                <div class="password-wrapper">
                    <input type="password" name="confirmPassword" id="confirmPassword" 
                           class="password-input" placeholder="Xác nhận mật khẩu mới" required />
                </div>
            </div>

            <!-- Yêu cầu mật khẩu -->
            <div class="password-requirements">
                <div class="password-requirements--gr">
                    <div class="password-requirements--item">
                        <img src="../Assets/images/icon/circle-outline.svg"
                            data-default="../Assets/images/icon/circle-outline.svg"
                            data-valid="../Assets/images/icon/circle-outline-green.svg"
                            alt="Circle" class="req-icon circle-outline" id="icon-length" />
                        <div class="req-item" id="req-length">Ít nhất 8 ký tự</div>
                    </div>
                    <div class="password-requirements--item">
                        <img src="../Assets/images/icon/circle-outline.svg"
                            data-default="../Assets/images/icon/circle-outline.svg"
                            data-valid="../Assets/images/icon/circle-outline-green.svg"
                            alt="Circle" class="req-icon circle-outline" id="icon-lowercase" />
                        <div class="req-item" id="req-lowercase">Một chữ viết thường</div>
                    </div>
                    <div class="password-requirements--item">
                        <img src="../Assets/images/icon/circle-outline.svg"
                            data-default="../Assets/images/icon/circle-outline.svg"
                            data-valid="../Assets/images/icon/circle-outline-green.svg"
                            alt="Circle" class="req-icon circle-outline" id="icon-special" />
                        <div class="req-item" id="req-special">Một ký tự đặc biệt (!@#$%...)</div>
                    </div>
                </div>
                <div class="password-requirements--gr">
                    <div class="password-requirements--item">
                        <img src="../Assets/images/icon/circle-outline.svg"
                            data-default="../Assets/images/icon/circle-outline.svg"
                            data-valid="../Assets/images/icon/circle-outline-green.svg"
                            alt="Circle" class="req-icon circle-outline" id="icon-uppercase" />
                        <div class="req-item" id="req-uppercase">Một chữ viết hoa</div>
                    </div>
                    <div class="password-requirements--item">
                        <img src="../Assets/images/icon/circle-outline.svg"
                            data-default="../Assets/images/icon/circle-outline.svg"
                            data-valid="../Assets/images/icon/circle-outline-green.svg"
                            alt="Circle" class="req-icon circle-outline" id="icon-number" />
                        <div class="req-item" id="req-number">Một số</div>
                    </div>
                </div>
            </div>

            <% if (!string.IsNullOrEmpty(Message)) { %>
                <div class="msg <%= MessageClass %>" style="display:block;"><%= Message %></div>
            <% } %>

            <button type="submit" name="btnAction" value="reset" class="btn-reset">
                Đặt lại mật khẩu
            </button>

        </div>
    </form>

    <script>
        function togglePasswordVisibility() {
            const newPass = document.getElementById('newPassword');
            const confirmPass = document.getElementById('confirmPassword');
            const icon = document.getElementById('toggleIcon');
            const isPassword = newPass.type === 'password';
            newPass.type = isPassword ? 'text' : 'password';
            confirmPass.type = isPassword ? 'text' : 'password';
            icon.src = isPassword ? '../Assets/images/icon/eye-open.svg' : '../Assets/images/icon/eye-close.svg';
        }

        function checkPasswordStrength() {
            const pass = document.getElementById('newPassword').value;
            const bar = document.getElementById('strengthBar');
            const text = document.getElementById('strengthText');

            // Nếu chưa nhập gì → reset về trạng thái mặc định
            if (pass === '') {
                bar.style.width = '0%';
                bar.style.backgroundColor = '#e4e6eb';
                text.textContent = 'Độ mạnh của mật khẩu';
                text.style.color = '#606770';

                // Reset tất cả icon về circle-outline.svg và bỏ class valid
                document.querySelectorAll('.req-icon').forEach(icon => {
                    icon.src = '../Assets/images/icon/circle-outline.svg';
                });
                document.querySelectorAll('.req-item').forEach(item => {
                    item.classList.remove('valid');
                });
                return;
            }

            let strength = 0;
            const requirements = [
                { id: 'req-length', iconId: 'icon-length', test: pass.length >= 8 },
                { id: 'req-lowercase', iconId: 'icon-lowercase', test: /[a-z]/.test(pass) },
                { id: 'req-uppercase', iconId: 'icon-uppercase', test: /[A-Z]/.test(pass) },
                { id: 'req-number', iconId: 'icon-number', test: /[0-9]/.test(pass) },
                { id: 'req-special', iconId: 'icon-special', test: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(pass) }
            ];

            requirements.forEach(req => {
                const el = document.getElementById(req.id);
                const icon = document.getElementById(req.iconId);

                if (req.test) {
                    el.classList.add('valid');
                    icon.src = '../Assets/images/icon/circle-outline-green.svg'; 
                    strength++;
                } else {
                    el.classList.remove('valid');
                    icon.src = '../Assets/images/icon/circle-outline.svg';
                }
            });

            // Cập nhật thanh độ mạnh
            let percentages, colors, texts;
            if (strength === 0) {
                percentages = [0];
                colors = ['#e4e6eb'];
                texts = ['Độ mạnh của mật khẩu'];
            } else if (strength === 1) {
                percentages = [20];
                colors = ['#dc3545'];
                texts = ['Rất yếu'];
            } else if (strength === 2) {
                percentages = [40];
                colors = ['#dc3545'];
                texts = ['Yếu'];
            } else if (strength === 3) {
                percentages = [60];
                colors = ['#ffc107'];
                texts = ['Trung bình'];
            } else if (strength === 4) {
                percentages = [80];
                colors = ['#28a745'];
                texts = ['Mạnh'];
            } else {
                percentages = [100];
                colors = ['#1e7e34'];
                texts = ['Rất mạnh'];
            }

            bar.style.width = percentages[0] + '%';
            bar.style.backgroundColor = colors[0];
            text.textContent = texts[0];
            text.style.color = strength === 0 ? '#606770' : colors[0];
        }
    </script>
</body>
</html>
