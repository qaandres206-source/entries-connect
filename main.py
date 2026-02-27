import warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)

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
class ConnectWiseConfig:
    """Configuración de ConnectWise"""
    def __init__(self, page: ft.Page):
        self.page = page
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

    async def load(self):
        """Carga la configuración del almacenamiento local de forma asíncrona"""
        self.company_id = await self.page.client_storage.get_async("company_id") or "Intwo"
        
        self.public_key = await self.page.client_storage.get_async("public_key") or ""
        self.private_key = await self.page.client_storage.get_async("private_key") or ""
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
        
        await self.page.client_storage.set_async("public_key", self.public_key)
        await self.page.client_storage.set_async("private_key", self.private_key)
        await self.page.client_storage.set_async("site_url", self.site_url)
        await self.page.client_storage.set_async("member_id", self.member_id)
        await self.page.client_storage.set_async("work_type", self.work_type)
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
                 billable_option: str,
                 add_to_detail: bool = False, add_to_internal: bool = True, add_to_resolution: bool = False,
                 email_resource: bool = False, email_contact: bool = False, email_cc: bool = False):
        self.ticket_id = ticket_id
        self.hours = hours
        self.description = description
        self.date = date
        self.billable_option = billable_option
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
                "billableOption": entry.billable_option,
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
    
    


    billable_dropdown = ft.Dropdown(
        label="Billable",
        width=150,
        options=[
            ft.dropdown.Option("Billable"),
            ft.dropdown.Option("DoNotBill", "Do Not Bill"),
            ft.dropdown.Option("NoCharge", "No Charge"),
        ],
        value="DoNotBill"
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
    
    # Lista de fechas y horas de inicio para múltiples entradas
    date_entries = []
    
    def create_date_entry_row(index: int):
        """Crea una fila de fecha con hora de inicio y botón de eliminar"""
        date_field = ft.TextField(
            label="Fecha",
            value=datetime.now().strftime("%Y-%m-%d"),
            width=150,
            hint_text="YYYY-MM-DD",
            prefix_icon=ft.Icons.CALENDAR_TODAY,
            data=index,
        )
        
        time_field = ft.TextField(
            label="Hora Inicio",
            hint_text="HH:MM",
            width=100,
            value="08:00",
            keyboard_type=ft.KeyboardType.DATETIME,
            data=index,
        )
        
        hours_display = ft.TextField(
            label="Horas",
            width=80,
            value="1.0",
            keyboard_type=ft.KeyboardType.NUMBER,
            data=index,
        )
        
        def remove_date_entry(e):
            """Elimina esta fila de fecha"""
            # Encontrar y eliminar de la lista
            for i, entry in enumerate(date_entries):
                if entry['index'] == index:
                    date_entries.pop(i)
                    break
            update_date_entries_ui()
        
        remove_btn = ft.IconButton(
            icon=ft.Icons.REMOVE_CIRCLE,
            icon_color=ft.Colors.RED_400,
            tooltip="Eliminar fecha",
            on_click=remove_date_entry,
            visible=len(date_entries) > 0,  # Solo visible si hay más de una fecha
        )
        
        row = ft.Row([
            date_field,
            time_field,
            hours_display,
            remove_btn,
        ], spacing=10)
        
        return {
            'index': index,
            'row': row,
            'date_field': date_field,
            'time_field': time_field,
            'hours_field': hours_display,
            'remove_btn': remove_btn,
        }
    
    # Contenedor para las filas de fechas
    date_entries_column = ft.Column(spacing=10)
    
    def update_date_entries_ui():
        """Actualiza la UI de las entradas de fecha"""
        date_entries_column.controls.clear()
        
        for entry in date_entries:
            # Actualizar visibilidad del botón de eliminar
            entry['remove_btn'].visible = len(date_entries) > 1
            date_entries_column.controls.append(entry['row'])
        
        # Solo actualizar si ya está en la página
        if date_entries_column.page:
            date_entries_column.update()
    
    def add_date_entry(e=None):
        """Agrega una nueva fila de fecha"""
        index = len(date_entries)
        new_entry = create_date_entry_row(index)
        date_entries.append(new_entry)
        update_date_entries_ui()
    
    # Agregar la primera fecha por defecto
    add_date_entry()
    
    # Botón para agregar más fechas
    add_date_btn = ft.Container(
        content=ft.IconButton(
            icon=ft.Icons.ADD_CIRCLE,
            icon_color=ft.Colors.WHITE,
            bgcolor=ft.Colors.BLUE_500,
            tooltip="Agregar otra fecha",
            on_click=add_date_entry,
        ),
        alignment=ft.alignment.center,
    )
    
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
        color = "error" if is_error else "onSurfaceVariant"
        
        log_list.controls.insert(0, ft.Container(
            content=ft.Row([
                ft.Icon(icon, color=color, size=16),
                ft.Text(message, size=12, color=color),
            ]),
            padding=5,
            bgcolor="errorContainer" if is_error else "surfaceVariant",
            border_radius=5,
        ))
        log_list.update()

    async def submit_entry(e):
        """Envía la entrada directamente para todas las fechas configuradas"""
        ticket_id = ticket_field.value.strip()
        
        if not ticket_id:
            show_snackbar("Por favor ingresa un Ticket ID", ft.Colors.RED)
            return
        
        # Validar que haya al menos una fecha
        if not date_entries:
            show_snackbar("Debe haber al menos una fecha", ft.Colors.RED)
            return
        
        description = description_field.value.strip()
        if not description:
            show_snackbar("Por favor ingresa una descripción", ft.Colors.RED)
            return
        
        # Deshabilitar botón mientras procesa
        submit_btn.disabled = True
        submit_btn.text = "Enviando..."
        submit_btn.update()
        
        success_count: int = 0
        error_count: int = 0
        
        try:
            # Procesar cada fecha
            for date_entry in date_entries:
                try:
                    # Parsear fecha
                    date_str = date_entry['date_field'].value.strip()
                    selected_date = datetime.strptime(date_str, "%Y-%m-%d")
                except ValueError:
                    add_log(f"Fecha inválida: {date_str}", is_error=True)
                    error_count += 1
                    continue
                
                try:
                    # Obtener horas desde el campo específico de esta fecha
                    hours = float(date_entry['hours_field'].value)
                    if hours <= 0 or hours > 8:
                        add_log(f"Horas inválidas para {date_str}: {hours}", is_error=True)
                        error_count += 1
                        continue
                except ValueError:
                    add_log(f"Horas inválidas para {date_str}", is_error=True)
                    error_count += 1
                    continue
                
                # Determinar hora de inicio
                start_hour = 8.0  # Default
                try:
                    st_val = date_entry['time_field'].value.strip()
                    if ":" in st_val:
                        parts = st_val.split(":")
                        h = int(parts[0])
                        m = int(parts[1])
                        start_hour = h + (m / 60.0)
                except:
                    pass  # Mantener default si falla
                
                # Crear objeto temporal para pasar a la API
                entry_obj = TimeEntry(
                    ticket_id, hours, description, selected_date,
                    billable_option=billable_dropdown.value,
                    add_to_detail=cb_discussion.value,
                    add_to_internal=cb_internal.value,
                    add_to_resolution=cb_resolution.value,
                    email_resource=cb_resource.value,
                    email_contact=cb_contact.value,
                    email_cc=cb_cc.value
                )
                
                # Enviar a la API
                is_success, message = await api.post_time_entry(entry_obj, start_hour)
                
                if is_success:
                    success_count += 1  # type: ignore
                    success_msg = f"✓ {date_str}: Ticket #{ticket_id} ({hours}h) - {message}"
                    add_log(success_msg)
                else:
                    error_count += 1  # type: ignore
                    error_msg = f"✗ {date_str}: Ticket #{ticket_id} - {message}"
                    add_log(error_msg, is_error=True)
            
            # Resumen final
            if success_count > 0 and error_count == 0:
                show_snackbar(f"✓ {success_count} entrada(s) registrada(s) exitosamente", ft.Colors.GREEN)
                # Limpiar campos
                description_field.value = ""
                ticket_field.value = ""
                # Resetear a una sola fecha
                date_entries.clear()
                add_date_entry()
                ticket_field.focus()
                page.update()
            elif success_count > 0 and error_count > 0:
                show_snackbar(f"Parcial: {success_count} exitosas, {error_count} errores", ft.Colors.ORANGE)
            else:
                show_snackbar(f"Error: {error_count} entrada(s) fallaron", ft.Colors.RED)
                
        except Exception as ex:
            show_snackbar(f"Error inesperado: {str(ex)}", ft.Colors.RED)
        finally:
            submit_btn.disabled = False
            submit_btn.text = "Registrar Entrada"
            submit_btn.update()


    
    def open_settings(e=None):
        """Abre el diálogo de configuración"""
        # Instanciar controles de texto de forma directa
        member_id_field = ft.TextField(label="Member ID", value=config.member_id, width=300, hint_text="Ej: username")
        public_key_field = ft.TextField(label="Public Key", value=config.public_key, width=300, password=True, can_reveal_password=True)
        private_key_field = ft.TextField(label="Private Key", value=config.private_key, width=300, password=True, can_reveal_password=True)

        company_id_field = ft.TextField(label="Company ID", value=config.company_id, width=300, password=True, can_reveal_password=True)
        site_url_field = ft.TextField(label="Site URL", value=config.site_url, width=300, password=True, can_reveal_password=True)
        client_id_field = ft.TextField(label="Client ID", value=config.client_id, width=300, password=True, can_reveal_password=True)
        timezone_field = ft.TextField(label="Timezone Offset", value=str(config.timezone_offset), width=300, hint_text="-4.0 para PR, -5.0 para COL", password=True, can_reveal_password=True)

        # Credenciales de Usuario (Prioridad)
        user_credentials = ft.Column([
            ft.Text("Credenciales de Usuario", weight=ft.FontWeight.BOLD, size=16),
            member_id_field,
            public_key_field,
            private_key_field,
        ], spacing=15)

        # Configuración General (Predefinida)
        general_config = ft.ExpansionTile(
            title=ft.Text("Configuración Avanzada"),
            subtitle=ft.Text("Company ID, Site URL, Client ID"),
            controls=[
                ft.Container(
                    content=ft.Column([
                        company_id_field,
                        site_url_field,
                        client_id_field,
                        timezone_field,
                    ], spacing=15),
                    padding=ft.padding.only(left=15, right=15, top=10, bottom=20)
                )
            ],
            initially_expanded=False
        )
        
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
                                billable_dropdown,
                            ], spacing=10, wrap=True),
                            
                            ft.Row([description_field]),
                            
                            filters_row,
                            
                            # Sección de fechas con título
                            ft.Container(
                                content=ft.Column([
                                    ft.Row([
                                        ft.Icon(ft.Icons.CALENDAR_MONTH, size=16),
                                        ft.Text("Fecha", size=14, weight=ft.FontWeight.BOLD),
                                        ft.Text(f"Rango: {min_date_str} a {max_date_str}", size=10, color="onSurfaceVariant"),
                                    ], spacing=5),
                                    date_entries_column,
                                    add_date_btn,
                                ], spacing=10),
                                padding=10,
                                bgcolor="surfaceVariant",
                                border_radius=8,
                            ),
                            
                            ft.Container(height=10),
                            
                            ft.Row([
                                submit_btn
                            ], alignment=ft.MainAxisAlignment.CENTER),
                            
                        ], spacing=15),
                        padding=20,
                        border=ft.border.all(1, "outlineVariant"),
                        border_radius=10,
                    ),
                    
                    ft.Divider(),
                    
                    # Log de sesión
                    ft.Text("Log de Sesión (Recientes)", size=16, weight=ft.FontWeight.BOLD),
                    ft.Container(
                        content=log_list,
                        padding=10,
                        bgcolor="surfaceVariant",
                        border_radius=5,
                        border=ft.border.all(1, "outlineVariant")
                    )
                    
                ], spacing=20, scroll=ft.ScrollMode.AUTO),
                padding=10,
                alignment=ft.alignment.center,
            )
        )
    )

    # Lógica de inicio
    # Abrir configuración automáticamente si faltan datos
    if not config.is_complete():
        open_settings()




if __name__ == "__main__":
    import os
    port = int(os.environ.get("PORT", 8080))
    ft.app(target=main, view=ft.AppView.WEB_BROWSER, port=port)
