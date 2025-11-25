import flet as ft
import base64
import json
from datetime import datetime, timedelta
from typing import List, Dict, Optional
import uuid
import asyncio

# Use httpx for API calls
import httpx
import os
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
import secrets


class SecurityManager:
    """Maneja la encriptación de credenciales"""
    def __init__(self):
        self.salt = b''
        self.fernet: Optional[Fernet] = None

    def generate_key_from_pin(self, pin: str, salt: bytes = None) -> bytes:
        """Genera una clave de encriptación basada en el PIN"""
        if salt is None:
            salt = secrets.token_bytes(16)
        self.salt = salt
        
        kdf = PBKDF2HMAC(
            algorithm=hashes.SHA256(),
            length=32,
            salt=salt,
            iterations=100000,
        )
        key = base64.urlsafe_b64encode(kdf.derive(pin.encode()))
        return key

    def initialize(self, pin: str, salt_hex: str = None) -> bool:
        """Inicializa el sistema de seguridad con un PIN"""
        try:
            if salt_hex:
                salt = bytes.fromhex(salt_hex)
            else:
                salt = secrets.token_bytes(16)
            
            key = self.generate_key_from_pin(pin, salt)
            self.fernet = Fernet(key)
            return True
        except Exception as e:
            print(f"Error initializing security: {e}")
            return False

    def encrypt(self, data: str) -> str:
        """Encripta un string"""
        if not self.fernet or not data: return data
        return self.fernet.encrypt(data.encode()).decode()

    def decrypt(self, data: str) -> str:
        """Desencripta un string"""
        if not self.fernet or not data: return data
        try:
            return self.fernet.decrypt(data.encode()).decode()
        except:
            return "" # Retorna vacío si falla la desencriptación

class ConnectWiseConfig:
    """Configuración de ConnectWise"""
    def __init__(self, page: ft.Page):
        self.page = page
        self.security = SecurityManager()
        self.is_locked = True
        # Inicializar con valores por defecto, se cargarán con load()
        self.company_id = "Intwo"
        self.public_key = ""
        self.private_key = ""
        self.site_url = "connect.intwo.cloud"
        self.member_id = ""
        self.work_type = "Remote-Standard"
        self.billable_option = "DoNotBill"
        self.client_id = "4332716b-7270-470d-b7c6-9c036f760e6f"
        self.timezone_offset = -4.0 # Default to Puerto Rico (UTC-4)

    async def unlock(self, pin: str) -> bool:
        """Intenta desbloquear la configuración con el PIN"""
        salt_hex = await self.page.client_storage.get_async("security_salt")
        if self.security.initialize(pin, salt_hex):
            self.is_locked = False
            await self.load()
            return True
        return False

    async def load(self):
        """Carga la configuración del almacenamiento local de forma asíncrona"""
        self.company_id = await self.page.client_storage.get_async("company_id") or "Intwo"
        
        # Cargar claves encriptadas
        enc_public = await self.page.client_storage.get_async("public_key") or ""
        enc_private = await self.page.client_storage.get_async("private_key") or ""
        
        if not self.is_locked:
            self.public_key = self.security.decrypt(enc_public)
            self.private_key = self.security.decrypt(enc_private)
        else:
            # Si está bloqueado, mantenemos las versiones encriptadas o vacías
            self.public_key = enc_public
            self.private_key = enc_private
        self.site_url = await self.page.client_storage.get_async("site_url") or "connect.intwo.cloud"
        self.member_id = await self.page.client_storage.get_async("member_id") or ""
        self.work_type = await self.page.client_storage.get_async("work_type") or "Remote-Standard"
        self.billable_option = await self.page.client_storage.get_async("billable_option") or "DoNotBill"
        self.billable_option = await self.page.client_storage.get_async("billable_option") or "DoNotBill"
        self.client_id = await self.page.client_storage.get_async("client_id") or "4332716b-7270-470d-b7c6-9c036f760e6f"
        try:
            self.timezone_offset = float(await self.page.client_storage.get_async("timezone_offset") or -4.0)
        except:
            self.timezone_offset = -4.0

    async def save(self):
        """Guarda la configuración en el almacenamiento local de forma asíncrona"""
        await self.page.client_storage.set_async("company_id", self.company_id)
        
        # Guardar Salt si es nuevo
        if self.security.salt:
            await self.page.client_storage.set_async("security_salt", self.security.salt.hex())
            
        # Encriptar antes de guardar
        if not self.is_locked:
            enc_public = self.security.encrypt(self.public_key)
            enc_private = self.security.encrypt(self.private_key)
            await self.page.client_storage.set_async("public_key", enc_public)
            await self.page.client_storage.set_async("private_key", enc_private)
        
        await self.page.client_storage.set_async("site_url", self.site_url)
        await self.page.client_storage.set_async("member_id", self.member_id)
        await self.page.client_storage.set_async("work_type", self.work_type)
        await self.page.client_storage.set_async("billable_option", self.billable_option)
        await self.page.client_storage.set_async("billable_option", self.billable_option)
        await self.page.client_storage.set_async("client_id", self.client_id)
        await self.page.client_storage.set_async("timezone_offset", self.timezone_offset)

    def is_complete(self) -> bool:
        """Verifica si la configuración crítica está completa"""
        return all([self.company_id, self.public_key, self.private_key, self.member_id, self.client_id])

    def get_auth_header(self) -> str:
        """Genera el header de autenticación"""
        auth_string = f"{self.company_id}+{self.public_key}:{self.private_key}"
        return base64.b64encode(auth_string.encode()).decode()


class TimeEntry:
    """Representa una entrada de tiempo"""
    def __init__(self, ticket_id: str, hours: float, description: str, date: datetime, 
                 add_to_detail: bool = False, add_to_internal: bool = True, add_to_resolution: bool = False,
                 email_resource: bool = False, email_contact: bool = False, email_cc: bool = False):
        self.ticket_id = ticket_id
        self.hours = hours
        self.description = description
        self.date = date
        self.add_to_detail = add_to_detail
        self.add_to_internal = add_to_internal
        self.add_to_resolution = add_to_resolution
        self.email_resource = email_resource
        self.email_contact = email_contact
        self.email_cc = email_cc
        self.id = str(uuid.uuid4())[:8]
        self.status = "pending"  # pending, success, error
        self.error_message = ""


class ConnectWiseAPI:
    """Maneja las llamadas a la API de ConnectWise"""
    def __init__(self, config: ConnectWiseConfig):
        self.config = config
        self.base_url = f"https://{config.site_url}/v4_6_release/apis/3.0"

    def get_headers(self) -> dict:
        """Obtiene los headers para las peticiones"""
        return {
            "Authorization": f"Basic {self.config.get_auth_header()}",
            "Content-Type": "application/json",
            "clientId": self.config.client_id
        }

    async def post_time_entry(self, entry: TimeEntry, start_hour: float) -> tuple[bool, str]:
        """Envía una entrada de tiempo a ConnectWise"""
        try:
            url = f"{self.base_url}/time/entries"
            
            # Calcular horas de inicio y fin
            end_hour = start_hour + entry.hours
            
            # Crear timestamps
            date_obj = entry.date
            start_time = datetime(
                date_obj.year, date_obj.month, date_obj.day,
                int(start_hour), int((start_hour % 1) * 60), 0
            )
            
            # Ajustar por Timezone Offset para obtener UTC
            # Si estoy en UTC-4 (PR) y son las 8:00, en UTC son las 12:00
            # UTC = Local - Offset => 8 - (-4) = 12
            start_time = start_time - timedelta(hours=self.config.timezone_offset)
            
            end_time = datetime(
                date_obj.year, date_obj.month, date_obj.day,
                int(end_hour), int((end_hour % 1) * 60), 0
            )
            # Ajustar fin también
            end_time = end_time - timedelta(hours=self.config.timezone_offset)
            
            # Formatear fechas en UTC
            time_start = start_time.strftime("%Y-%m-%dT%H:%M:%SZ")
            time_end = end_time.strftime("%Y-%m-%dT%H:%M:%SZ")
            
            payload = {
                "company": {"identifier": self.config.company_id},
                "chargeToId": int(entry.ticket_id),
                "chargeToType": "ServiceTicket",
                "member": {"identifier": self.config.member_id},
                "actualHours": entry.hours,
                "billableOption": self.config.billable_option,
                "workType": {"name": self.config.work_type},
                "notes": entry.description,
                "timeStart": time_start,
                "timeEnd": time_end,
                "addToDetailDescriptionFlag": entry.add_to_detail,
                "addToInternalAnalysisFlag": entry.add_to_internal,
                "addToResolutionFlag": entry.add_to_resolution,
                "emailResourceFlag": entry.email_resource,
                "emailContactFlag": entry.email_contact,
                "emailCcFlag": entry.email_cc,
            }
            
            # Use httpx for all API calls
            async with httpx.AsyncClient() as client:
                response = await client.post(
                    url, 
                    headers=self.get_headers(), 
                    json=payload, 
                    timeout=15.0
                )
            
            if response.status_code in [200, 201]:
                return True, "Entrada creada exitosamente"
            else:
                error_data = response.json() if response.text else {}
                error_msg = error_data.get("message", response.text)
                return False, f"Error {response.status_code}: {error_msg}"
                
        except Exception as e:
            return False, f"Error: {str(e)}"


async def main(page: ft.Page):
    page.title = "ConnectWise Time Entry"
    page.theme_mode = ft.ThemeMode.LIGHT
    page.padding = 20
    page.scroll = ft.ScrollMode.AUTO
    
    # Estado de la aplicación
    config = ConnectWiseConfig(page)
    # Cargar configuración de forma asíncrona
    await config.load()
    
    api = ConnectWiseAPI(config)
    # Rastrear hora de inicio por fecha: "YYYY-MM-DD" -> float (hora)
    day_tracker: Dict[str, float] = {}
    session_log: List[str] = []
    
    # Controles de la interfaz
    ticket_field = ft.TextField(
        label="Ticket ID",
        hint_text="Ej: 12345",
        width=200,
        keyboard_type=ft.KeyboardType.NUMBER,
        autofocus=True
    )
    
    hours_field = ft.TextField(
        label="Horas",
        hint_text="1.0 - 8.0",
        width=100,
        keyboard_type=ft.KeyboardType.NUMBER,
        value="1.0"
    )
    
    start_time_field = ft.TextField(
        label="Hora Inicio",
        hint_text="HH:MM (24h)",
        width=100,
        value="08:00",
        keyboard_type=ft.KeyboardType.DATETIME
    )
    
    description_field = ft.TextField(
        label="Descripción",
        hint_text="Descripción de la actividad",
        multiline=True,
        max_lines=3,
        expand=True,
    )
    
    # Checkboxes for filters
    cb_discussion = ft.Checkbox(label="Discussion", value=False)
    cb_internal = ft.Checkbox(label="Internal", value=True)
    cb_resolution = ft.Checkbox(label="Resolution", value=False)
    
    # Checkboxes for notifications
    cb_resource = ft.Checkbox(label="Resource", value=False)
    cb_contact = ft.Checkbox(label="Contact", value=False)
    cb_cc = ft.Checkbox(label="CC", value=False)
    
    filters_row = ft.Row([
        cb_discussion,
        cb_internal,
        cb_resolution,
        ft.VerticalDivider(),
        ft.Text("Notify:", size=12, weight=ft.FontWeight.BOLD),
        cb_resource,
        cb_contact,
        cb_cc
    ], spacing=10, scroll=ft.ScrollMode.AUTO)
    
    
    # Calcular rango de fechas permitido (mes actual y anterior)
    today = datetime.now()
    first_day_current_month = datetime(today.year, today.month, 1)
    # Calcular primer día del mes anterior
    if today.month == 1:
        first_day_prev_month = datetime(today.year - 1, 12, 1)
    else:
        first_day_prev_month = datetime(today.year, today.month - 1, 1)
    
    min_date_str = first_day_prev_month.strftime("%Y-%m-%d")
    max_date_str = today.strftime("%Y-%m-%d")
    
    selected_date = datetime.now()
    
    # Campo de fecha editable
    date_field = ft.TextField(
        label="Fecha",
        value=selected_date.strftime("%Y-%m-%d"),
        width=200,
        hint_text="YYYY-MM-DD",
        prefix_icon=ft.Icons.CALENDAR_TODAY,
        helper_text=f"Rango: {min_date_str} a {max_date_str}",
    )
    
    def parse_date_from_field():
        """Parsea la fecha del campo de texto"""
        nonlocal selected_date
        try:
            date_str = date_field.value
            selected_date = datetime.strptime(date_str, "%Y-%m-%d")
            return True
        except:
            return False
    
    log_list = ft.Column(
        spacing=5,
        scroll=ft.ScrollMode.AUTO,
        height=200,
    )
    
    def show_snackbar(message: str, color: str = ft.Colors.GREEN):
        page.open(ft.SnackBar(
            content=ft.Text(message),
            bgcolor=color,
        ))
    
    def add_log(message: str, is_error: bool = False):
        """Agrega un mensaje al log de sesión"""
        icon = ft.Icons.ERROR if is_error else ft.Icons.CHECK_CIRCLE
        color = ft.Colors.RED_700 if is_error else ft.Colors.GREEN_700
        
        log_list.controls.insert(0, ft.Container(
            content=ft.Row([
                ft.Icon(icon, color=color, size=16),
                ft.Text(message, size=12, color=ft.Colors.GREY_800 if not is_error else ft.Colors.RED_900),
            ]),
            padding=5,
            bgcolor=ft.Colors.GREY_100 if not is_error else ft.Colors.RED_50,
            border_radius=5,
        ))
        log_list.update()

    async def submit_entry(e):
        """Envía la entrada directamente"""
        ticket_id = ticket_field.value.strip()
        
        if not ticket_id:
            show_snackbar("Por favor ingresa un Ticket ID", ft.Colors.RED)
            return
        
        # Parsear fecha del campo
        if not parse_date_from_field():
            show_snackbar("Por favor ingresa una fecha válida (YYYY-MM-DD)", ft.Colors.RED)
            return
        
        try:
            hours = float(hours_field.value)
            if hours <= 0 or hours > 8:
                show_snackbar("Las horas deben estar entre 0 y 8", ft.Colors.RED)
                return
        except ValueError:
            show_snackbar("Por favor ingresa un valor numérico válido", ft.Colors.RED)
            return
        
        description = description_field.value.strip()
        if not description:
            show_snackbar("Por favor ingresa una descripción", ft.Colors.RED)
            return
            
        # Determinar hora de inicio
        date_key = selected_date.strftime("%Y-%m-%d")
        if date_key not in day_tracker:
            day_tracker[date_key] = 8.0 # Empezar a las 8:00 AM
            
        start_hour = day_tracker[date_key]
        
        # Sobrescribir con el campo manual si es válido
        try:
            st_val = start_time_field.value.strip()
            if ":" in st_val:
                parts = st_val.split(":")
                h = int(parts[0])
                m = int(parts[1])
                start_hour = h + (m / 60.0)
        except:
            pass # Mantener automático si falla
            
        # Actualizar tracker para la SIGUIENTE entrada
        day_tracker[date_key] = start_hour + hours
        
        # Actualizar visualmente el campo de hora para la siguiente
        next_h = int(day_tracker[date_key])
        next_m = int((day_tracker[date_key] % 1) * 60)
        start_time_field.value = f"{next_h:02d}:{next_m:02d}"
        start_time_field.update()
        
        # Crear objeto temporal para pasar a la API
        entry = TimeEntry(
            ticket_id, hours, description, selected_date,
            add_to_detail=cb_discussion.value,
            add_to_internal=cb_internal.value,
            add_to_resolution=cb_resolution.value,
            email_resource=cb_resource.value,
            email_contact=cb_contact.value,
            email_cc=cb_cc.value
        )
        
        # Deshabilitar botón mientras procesa
        submit_btn.disabled = True
        submit_btn.text = "Enviando..."
        submit_btn.update()
        
        try:
            is_success, message = await api.post_time_entry(entry, start_hour)
            
            if is_success:
                # Actualizar tracker (ya se hizo arriba, pero confirmamos)
                # day_tracker[date_key] += hours
                
                # Log y Feedback
                success_msg = f"Ticket #{ticket_id} ({hours}h) - {message}"
                add_log(success_msg)
                show_snackbar("Entrada registrada exitosamente", ft.Colors.GREEN)
                
                # Limpiar campos
                description_field.value = ""
                ticket_field.focus()
                page.update()
            else:
                error_msg = f"Error en Ticket #{ticket_id}: {message}"
                add_log(error_msg, is_error=True)
                show_snackbar(f"Error: {message}", ft.Colors.RED)
                
        except Exception as ex:
            show_snackbar(f"Error inesperado: {str(ex)}", ft.Colors.RED)
        finally:
            submit_btn.disabled = False
            submit_btn.text = "Registrar Entrada"
            submit_btn.update()

    
    def open_settings(e=None):
        """Abre el diálogo de configuración"""
        # Credenciales de Usuario (Prioridad)
        user_credentials = ft.Column([
            ft.Text("Credenciales de Usuario", weight=ft.FontWeight.BOLD, size=16),
            ft.TextField(label="Member ID", value=config.member_id, width=300, hint_text="Ej: amora"),
            ft.TextField(label="Public Key", value=config.public_key, width=300, password=True, can_reveal_password=True),
            ft.TextField(label="Private Key", value=config.private_key, width=300, password=True, can_reveal_password=True),
        ], spacing=10)

        # Configuración General (Predefinida)
        general_config = ft.ExpansionTile(
            title=ft.Text("Configuración Avanzada"),
            subtitle=ft.Text("Company ID, Site URL, Client ID"),
            controls=[
                ft.TextField(label="Company ID", value=config.company_id, width=300),
                ft.TextField(label="Site URL", value=config.site_url, width=300),
                ft.TextField(label="Client ID", value=config.client_id, width=300),
                ft.TextField(label="Timezone Offset", value=str(config.timezone_offset), width=300, hint_text="-4.0 para PR, -5.0 para COL"),
            ],
            initially_expanded=False
        )

        # Update references in save_settings
        member_id_field = user_credentials.controls[1]
        public_key_field = user_credentials.controls[2]
        private_key_field = user_credentials.controls[3]
        
        company_id_field = general_config.controls[0]
        site_url_field = general_config.controls[1]
        client_id_field = general_config.controls[2]
        timezone_field = general_config.controls[3]
        
        async def save_settings(se):
            config.company_id = company_id_field.value
            config.public_key = public_key_field.value
            config.private_key = private_key_field.value
            config.site_url = site_url_field.value
            config.member_id = member_id_field.value
            config.client_id = client_id_field.value
            try:
                config.timezone_offset = float(timezone_field.value)
            except:
                config.timezone_offset = -4.0
            
            await config.save()
            
            nonlocal api
            api = ConnectWiseAPI(config)
            
            page.close(settings_dialog)
            show_snackbar("Configuración guardada", ft.Colors.GREEN)
        
        settings_dialog = ft.AlertDialog(
            title=ft.Text("⚙️ Configuración"),
            content=ft.Column([
                user_credentials,
                ft.Divider(),
                general_config,
            ], tight=True, scroll=ft.ScrollMode.AUTO, height=500),
            actions=[
                ft.TextButton("Cancelar", on_click=lambda _: page.close(settings_dialog)),
                ft.ElevatedButton("Guardar", on_click=save_settings),
            ],
        )
        
        page.open(settings_dialog)


    
    def update_appbar():
        """Actualiza el AppBar según el tema actual"""
        is_dark = page.theme_mode == ft.ThemeMode.DARK
        page.appbar = ft.AppBar(
            leading=ft.Icon(ft.Icons.ACCESS_TIME_FILLED, color=ft.Colors.WHITE),
            leading_width=40,
            title=ft.Text("ConnectWise Time Entry", color=ft.Colors.WHITE),
            center_title=False,
            bgcolor=ft.Colors.BLUE_700 if not is_dark else ft.Colors.BLUE_900,
            actions=[
                ft.IconButton(
                    icon=ft.Icons.SETTINGS,
                    icon_color=ft.Colors.WHITE,
                    on_click=open_settings,
                    tooltip="Configuración"
                ),
                ft.IconButton(
                    icon=ft.Icons.BRIGHTNESS_6 if is_dark else ft.Icons.NIGHTLIGHT_ROUND,
                    icon_color=ft.Colors.WHITE,
                    on_click=lambda e: toggle_theme(),
                    tooltip="Cambiar tema"
                ),
            ],
        )
        page.update()
    
    def toggle_theme():
        """Cambia entre modo claro y oscuro"""
        page.theme_mode = ft.ThemeMode.DARK if page.theme_mode == ft.ThemeMode.LIGHT else ft.ThemeMode.LIGHT
        update_appbar()
    
    # Inicializar AppBar
    update_appbar()

    submit_btn = ft.ElevatedButton(
        "Registrar Entrada",
        on_click=submit_entry,
        icon=ft.Icons.SEND,
        style=ft.ButtonStyle(
            bgcolor=ft.Colors.BLUE_700,
            color=ft.Colors.WHITE,
            padding=15,
        ),
        width=200
    )

    # Construcción de la interfaz
    page.add(
        ft.SafeArea(
            ft.Container(
                content=ft.Column([
                    # Formulario
                    ft.Container(
                        content=ft.Column([
                            ft.Text("Nueva Entrada", size=20, weight=ft.FontWeight.BOLD),
                            
                            ft.Row([
                                ticket_field,
                                start_time_field,
                                hours_field,
                            ], spacing=10, wrap=True),
                            
                            ft.Row([description_field]),
                            
                            filters_row,
                            
                            ft.Row([
                                date_field,
                            ], spacing=10),
                            
                            ft.Container(height=10),
                            
                            ft.Row([
                                submit_btn
                            ], alignment=ft.MainAxisAlignment.CENTER),
                            
                        ], spacing=15),
                        padding=20,
                        border=ft.border.all(1, ft.Colors.GREY_300),
                        border_radius=10,
                    ),
                    
                    ft.Divider(),
                    
                    # Log de sesión
                    ft.Text("Log de Sesión (Recientes)", size=16, weight=ft.FontWeight.BOLD),
                    ft.Container(
                        content=log_list,
                        padding=10,
                        bgcolor=ft.Colors.GREY_50,
                        border_radius=5,
                        border=ft.border.all(1, ft.Colors.GREY_200)
                    )
                    
                ], spacing=20, scroll=ft.ScrollMode.AUTO),
                padding=10,
                alignment=ft.alignment.center,
            )
        )
    )
    
    # Abrir configuración automáticamente si faltan datos
    # if not config.is_complete():
    #    open_settings()

    def show_pin_dialog(is_setup=False):
        """Muestra el diálogo de PIN"""
        pin_field = ft.TextField(label="PIN de Seguridad", password=True, width=200, autofocus=True, on_submit=lambda e: validate_pin())
        error_text = ft.Text("", color=ft.Colors.RED, size=12)
        
        async def validate_pin(e=None):
            pin = pin_field.value
            if not pin:
                error_text.value = "Ingresa un PIN"
                error_text.update()
                return
                
            if is_setup:
                # Setup mode: Initialize with this PIN
                if config.security.initialize(pin):
                    config.is_locked = False
                    # Save empty config to store salt/setup
                    await config.save()
                    page.close(pin_dialog)
                    open_settings() # Go to settings to fill data
                else:
                    error_text.value = "Error al inicializar seguridad"
                    error_text.update()
            else:
                # Unlock mode
                if await config.unlock(pin):
                    page.close(pin_dialog)
                    show_snackbar("Desbloqueado correctamente", ft.Colors.GREEN)
                    if not config.is_complete():
                        open_settings()
                else:
                    error_text.value = "PIN Incorrecto"
                    error_text.update()
        
        title = "🛡️ Configurar Seguridad" if is_setup else "🔒 Desbloquear"
        content_text = "Crea un PIN para proteger tus claves." if is_setup else "Ingresa tu PIN para desbloquear."
        
        pin_dialog = ft.AlertDialog(
            title=ft.Text(title),
            content=ft.Column([
                ft.Text(content_text),
                pin_field,
                error_text
            ], tight=True),
            actions=[
                ft.ElevatedButton("Aceptar", on_click=validate_pin)
            ],
            modal=True
        )
        page.open(pin_dialog)

    # Lógica de inicio
    # Verificar si ya existe un salt (indicador de que ya se configuró seguridad)
    has_security = await page.client_storage.contains_key_async("security_salt")
    
    if has_security:
        show_pin_dialog(is_setup=False)
    else:
        # Primera vez
        show_pin_dialog(is_setup=True)




if __name__ == "__main__":
    import os
    port = int(os.environ.get("PORT", 8080))
    ft.app(target=main, view=ft.AppView.WEB_BROWSER, port=port)
