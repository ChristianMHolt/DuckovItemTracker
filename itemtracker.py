import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import json
import os

try:
    from PIL import Image, ImageTk
    PIL_AVAILABLE = True
except ImportError:
    PIL_AVAILABLE = False


BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_FILE = "items.json"
DATA_FILE_PATH = os.path.join(BASE_DIR, DATA_FILE)


class Item:
    def __init__(self, name, unit_price, stack_size, weight_per_item, icon_path=None):
        self.name = name
        self.unit_price = float(unit_price)
        self.stack_size = int(stack_size)
        self.weight_per_item = float(weight_per_item)
        self.icon_path = icon_path or ""

    @property
    def price_per_stack(self):
        return self.unit_price * self.stack_size if self.stack_size else 0.0

    @property
    def price_per_kg(self):
        # Avoid division by zero
        return self.unit_price / self.weight_per_item if self.weight_per_item else 0.0

    def to_dict(self):
        return {
            "name": self.name,
            "unit_price": self.unit_price,
            "stack_size": self.stack_size,
            "weight_per_item": self.weight_per_item,
            "icon_path": self.icon_path,
        }

    @classmethod
    def from_dict(cls, data):
        return cls(
            data.get("name", ""),
            data.get("unit_price", 0.0),
            data.get("stack_size", 1),
            data.get("weight_per_item", 1.0),
            data.get("icon_path", ""),
        )


class ItemTrackerApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Game Item Price Tracker")
        self.root.geometry("1000x600")

        # Set row height so 32x32 item icons have enough vertical space and do not overlap
        style = ttk.Style(self.root)
        style.configure("Treeview", rowheight=40)

        self.items = []  # list[Item]
        self.icon_cache = {}  # cache PhotoImage objects by icon path
        self.current_item_index = None
        self.current_icon_path = ""
        self.filtered_indices = []

        self.create_widgets()
        self.load_items()

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def create_widgets(self):
        main_frame = ttk.Frame(self.root, padding=10)
        main_frame.pack(fill="both", expand=True)

        # --- Top row: form + totals ---
        top_row = ttk.Frame(main_frame)
        top_row.pack(fill="x")

        # --- Form frame ---
        form_frame = ttk.LabelFrame(top_row, text="Item Details", padding=10)
        form_frame.pack(side="left", fill="x", expand=True, padx=5, pady=5)

        # Totals label aligned to the top-right
        self.totals_var = tk.StringVar(value="Total items: 0 (showing 0)")
        totals_bar = ttk.Label(top_row, textvariable=self.totals_var, anchor="e")
        totals_bar.pack(side="right", padx=5, pady=(5, 0))

        # Name
        ttk.Label(form_frame, text="Name:").grid(row=0, column=0, sticky="w")
        self.name_var = tk.StringVar()
        ttk.Entry(form_frame, textvariable=self.name_var, width=25).grid(row=0, column=1, sticky="w", padx=5)

        # Unit price
        ttk.Label(form_frame, text="Unit Price:").grid(row=0, column=2, sticky="w")
        self.price_var = tk.StringVar()
        ttk.Entry(form_frame, textvariable=self.price_var, width=10).grid(row=0, column=3, sticky="w", padx=5)

        # Stack size
        ttk.Label(form_frame, text="Stack Size:").grid(row=0, column=4, sticky="w")
        self.stack_var = tk.StringVar(value="1")
        ttk.Entry(form_frame, textvariable=self.stack_var, width=8).grid(row=0, column=5, sticky="w", padx=5)

        # Weight per item
        ttk.Label(form_frame, text="Weight per item (kg):").grid(row=1, column=0, sticky="w")
        self.weight_var = tk.StringVar(value="1.0")
        ttk.Entry(form_frame, textvariable=self.weight_var, width=10).grid(row=1, column=1, sticky="w", padx=5)

        # Icon selector
        self.icon_label_var = tk.StringVar(value="No icon selected")
        self.choose_icon_btn = ttk.Button(form_frame, text="Choose Icon", command=self.choose_icon)
        self.choose_icon_btn.grid(row=1, column=2, sticky="w", padx=5)
        ttk.Label(form_frame, textvariable=self.icon_label_var).grid(row=1, column=3, columnspan=3, sticky="w")

        # Buttons
        btn_frame = ttk.Frame(form_frame)
        btn_frame.grid(row=2, column=0, columnspan=6, sticky="w", pady=(10, 0))

        self.add_update_btn = ttk.Button(btn_frame, text="Add Item", command=self.add_or_update_item)
        self.add_update_btn.pack(side="left")

        self.clear_btn = ttk.Button(btn_frame, text="Clear Form", command=self.clear_form)
        self.clear_btn.pack(side="left", padx=5)
        self.delete_btn = ttk.Button(btn_frame, text="Delete Selected", command=self.delete_selected)
        self.delete_btn.pack(side="left", padx=5)

        for button in (self.choose_icon_btn, self.add_update_btn, self.clear_btn, self.delete_btn):
            self.bind_button_to_enter(button)

        # --- Controls row: sorting + search ---
        controls_row = ttk.Frame(main_frame)
        controls_row.pack(fill="x", padx=5, pady=5)

        # Sorting section
        sort_frame = ttk.LabelFrame(controls_row, text="Sorting", padding=10)
        sort_frame.pack(side="left", padx=(0, 5))

        ttk.Label(sort_frame, text="Sort by:").pack(side="left")
        self.sort_field_var = tk.StringVar(value="Name")
        sort_field_cb = ttk.Combobox(
            sort_frame,
            textvariable=self.sort_field_var,
            values=["Name", "Unit Price", "Price per Stack", "Price per kg"],
            state="readonly",
            width=15,
        )
        sort_field_cb.pack(side="left", padx=5)

        ttk.Label(sort_frame, text="Order:").pack(side="left", padx=(20, 0))
        self.sort_order_var = tk.StringVar(value="Ascending")
        sort_order_cb = ttk.Combobox(
            sort_frame,
            textvariable=self.sort_order_var,
            values=["Ascending", "Descending"],
            state="readonly",
            width=10,
        )
        sort_order_cb.pack(side="left", padx=5)

        ttk.Button(sort_frame, text="Apply Sort", command=self.apply_sort).pack(side="left", padx=10)

        # Search section
        search_frame = ttk.LabelFrame(controls_row, text="Search", padding=10)
        search_frame.pack(side="left", fill="x", expand=True, padx=(5, 0))

        ttk.Label(search_frame, text="Search items:").pack(side="left")
        self.search_var = tk.StringVar()
        search_entry = ttk.Entry(search_frame, textvariable=self.search_var, width=30)
        search_entry.pack(side="left", padx=5, fill="x", expand=True)
        self.search_var.trace_add("write", self.on_search_change)

        ttk.Button(search_frame, text="Clear Search", command=self.clear_search).pack(side="left", padx=5)

        # --- Treeview frame ---
        tree_frame = ttk.Frame(main_frame)
        tree_frame.pack(fill="both", expand=True, padx=5, pady=5)

        columns = ("unit_price", "stack_size", "price_per_stack", "weight_per_item", "price_per_kg")
        self.tree = ttk.Treeview(
            tree_frame,
            columns=columns,
            show="tree headings",
            height=15,
        )

        # Tree (icon + name) column
        self.tree.heading("#0", text="Item")
        self.tree.column("#0", width=200, anchor="w")

        self.tree.heading("unit_price", text="Unit Price")
        self.tree.heading("stack_size", text="Stack Size")
        self.tree.heading("price_per_stack", text="Price/Stack")
        self.tree.heading("weight_per_item", text="Weight/Item (kg)")
        self.tree.heading("price_per_kg", text="Price/kg")

        self.tree.column("unit_price", width=80, anchor="e")
        self.tree.column("stack_size", width=80, anchor="center")
        self.tree.column("price_per_stack", width=100, anchor="e")
        self.tree.column("weight_per_item", width=110, anchor="e")
        self.tree.column("price_per_kg", width=80, anchor="e")

        vsb = ttk.Scrollbar(tree_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=vsb.set)

        self.tree.pack(side="left", fill="both", expand=True)
        vsb.pack(side="right", fill="y")

        self.tree.bind("<<TreeviewSelect>>", self.on_tree_select)

        # Info label
        self.status_var = tk.StringVar(value="Ready")
        status_bar = ttk.Label(main_frame, textvariable=self.status_var, anchor="w")
        status_bar.pack(fill="x", pady=(5, 0))

    def choose_icon(self):
        filetypes = [
            ("Image files", "*.png *.jpg *.jpeg *.gif *.bmp"),
            ("All files", "*.*"),
        ]
        default_dir = os.path.join(BASE_DIR, "ItemPNGS")
        initial_dir = default_dir if os.path.isdir(default_dir) else BASE_DIR
        filename = filedialog.askopenfilename(
            title="Select icon image",
            filetypes=filetypes,
            initialdir=initial_dir,
        )
        if filename:
            self.current_icon_path = filename
            self.icon_label_var.set(os.path.basename(filename))

    def add_or_update_item(self):
        name = self.name_var.get().strip()
        if not name:
            messagebox.showwarning("Missing name", "Please enter an item name.")
            return

        try:
            unit_price = float(self.price_var.get())
            stack_size = int(self.stack_var.get())
            weight_per_item = float(self.weight_var.get())
        except ValueError:
            messagebox.showerror("Invalid number", "Please enter valid numeric values for price, stack size, and weight.")
            return

        if stack_size <= 0:
            messagebox.showerror("Invalid stack size", "Stack size must be greater than zero.")
            return
        if weight_per_item <= 0:
            messagebox.showerror("Invalid weight", "Weight per item must be greater than zero.")
            return

        new_item = Item(name, unit_price, stack_size, weight_per_item, self.current_icon_path)

        # Enforce unique item names (case-insensitive)
        existing_index = next(
            (i for i, item in enumerate(self.items) if item.name.lower() == name.lower()),
            None,
        )
        if existing_index is not None and existing_index != self.current_item_index:
            messagebox.showerror("Duplicate item", f"An item named '{name}' already exists.")
            return

        if self.current_item_index is None:
            # Add new
            self.items.append(new_item)
            self.status_var.set(f"Added item: {name}")
        else:
            # Update existing
            self.items[self.current_item_index] = new_item
            self.status_var.set(f"Updated item: {name}")

        self.update_filter()
        self.save_items()
        self.clear_form(keep_status=True)

    def delete_selected(self):
        selected_index = self.get_selected_index()
        if selected_index is None:
            messagebox.showinfo("No selection", "Please select an item to delete.")
            return

        item_name = self.items[selected_index].name
        if messagebox.askyesno("Confirm delete", f"Delete '{item_name}'?"):
            del self.items[selected_index]
            self.update_filter()
            self.save_items()
            self.clear_form()
            self.status_var.set(f"Deleted item: {item_name}")

    def clear_form(self, keep_status=False):
        self.name_var.set("")
        self.price_var.set("")
        self.stack_var.set("1")
        self.weight_var.set("1.0")
        self.current_icon_path = ""
        self.icon_label_var.set("No icon selected")
        self.current_item_index = None
        self.add_update_btn.config(text="Add Item")
        if not keep_status:
            self.status_var.set("Form cleared")

    def on_tree_select(self, event):
        index = self.get_selected_index()
        if index is None:
            return
        item = self.items[index]

        self.current_item_index = index
        self.name_var.set(item.name)
        self.price_var.set(f"{item.unit_price:.2f}")
        self.stack_var.set(str(item.stack_size))
        self.weight_var.set(f"{item.weight_per_item:.3f}")
        self.current_icon_path = item.icon_path or ""
        if self.current_icon_path:
            self.icon_label_var.set(os.path.basename(self.current_icon_path))
        else:
            self.icon_label_var.set("No icon selected")
        self.add_update_btn.config(text="Update Item")
        self.status_var.set(f"Editing item: {item.name}")

    def get_photoimage_for_item(self, item_index):
        item = self.items[item_index]
        icon_path = item.icon_path
        if not icon_path or not os.path.isfile(icon_path):
            return None

        if icon_path in self.icon_cache:
            return self.icon_cache[icon_path]

        try:
            if PIL_AVAILABLE:
                img = Image.open(icon_path)
                img.thumbnail((32, 32), Image.LANCZOS)
                photo = ImageTk.PhotoImage(img)
            else:
                # Fallback: Tk's PhotoImage supports only a few formats (e.g. GIF/PGM/PPM)
                photo = tk.PhotoImage(file=icon_path)
        except Exception:
            return None

        self.icon_cache[icon_path] = photo
        return photo

    def insert_tree_item(self, index):
        item = self.items[index]
        photo = self.get_photoimage_for_item(index)
        values = (
            f"{item.unit_price:.2f}",
            str(item.stack_size),
            f"{item.price_per_stack:.2f}",
            f"{item.weight_per_item:.3f}",
            f"{item.price_per_kg:.2f}",
        )
        self.tree.insert("", "end", iid=str(index), text=item.name, image=photo, values=values)

    def refresh_tree(self):
        self.tree.delete(*self.tree.get_children())
        for i in self.filtered_indices:
            self.insert_tree_item(i)

    def apply_sort(self):
        field = self.sort_field_var.get()
        order = self.sort_order_var.get()

        reverse = order == "Descending"

        if field == "Name":
            key_func = lambda it: it.name.lower()
        elif field == "Unit Price":
            key_func = lambda it: it.unit_price
        elif field == "Price per Stack":
            key_func = lambda it: it.price_per_stack
        else:
            key_func = lambda it: it.price_per_kg

        self.items.sort(key=key_func, reverse=reverse)
        self.update_filter()
        self.status_var.set(f"Sorted by {field} ({order.lower()})")

    def load_items(self):
        if not os.path.isfile(DATA_FILE_PATH):
            self.update_filter()
            return
        try:
            with open(DATA_FILE_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
            self.items = [Item.from_dict(d) for d in data]
            self.update_filter()
            self.status_var.set(f"Loaded {len(self.items)} items from {DATA_FILE}")
        except Exception as e:
            messagebox.showerror("Load error", f"Could not load items from {DATA_FILE}:\n{e}")

    def save_items(self):
        data = [item.to_dict() for item in self.items]
        try:
            with open(DATA_FILE_PATH, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
        except Exception as e:
            messagebox.showerror("Save error", f"Could not save items to {DATA_FILE}:\n{e}")

    def on_close(self):
        self.save_items()
        self.root.destroy()

    def update_filter(self, show_message=False):
        query = self.search_var.get().strip().lower() if hasattr(self, "search_var") else ""
        if query:
            self.filtered_indices = [i for i, item in enumerate(self.items) if query in item.name.lower()]
        else:
            self.filtered_indices = list(range(len(self.items)))

        self.refresh_tree()
        self.update_totals()

        if show_message:
            if query:
                self.status_var.set(f"Found {len(self.filtered_indices)} item(s) matching '{self.search_var.get().strip()}'")
            else:
                self.status_var.set("Showing all items")

    def update_totals(self):
        total_items = len(self.items)
        showing_items = len(self.filtered_indices)
        self.totals_var.set(f"Total items: {total_items} (showing {showing_items})")

    def on_search_change(self, *args):
        self.update_filter(show_message=True)

    def clear_search(self):
        self.search_var.set("")

    def bind_button_to_enter(self, button):
        button.bind("<Return>", lambda event: button.invoke())
        button.bind("<KP_Enter>", lambda event: button.invoke())

    def get_selected_index(self):
        sel = self.tree.selection()
        if not sel:
            return None
        try:
            index = int(sel[0])
        except (TypeError, ValueError):
            return None
        if 0 <= index < len(self.items):
            return index
        return None


def main():
    root = tk.Tk()
    app = ItemTrackerApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
