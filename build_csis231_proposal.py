from reportlab.lib.colors import HexColor, white, black
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    KeepTogether, PageBreak, Flowable, ListFlowable, ListItem,
)
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

OUT = r"d:\downloadss\CSIS231_A1_Proposal_AnthonyGerges.pdf"
NAVY = HexColor("#1F3A5F")
LINE = HexColor("#2E4A6E")
FILL = HexColor("#E8EEF6")
HEAD = HexColor("#1A3354")
LIGHT = HexColor("#F4F7FB")
ACCENT = HexColor("#2E6B4F")


class ERDFigure(Flowable):
    def __init__(self, width, height):
        super().__init__()
        self.width = width
        self.height = height

    def draw(self):
        c = self.canv
        boxes = {
            "student": (8, 132, 148, 86),
            "staff": (348, 132, 148, 86),
            "loan": (178, 118, 148, 108),
            "equipment": (178, 12, 148, 92),
            "category": (8, 18, 148, 76),
            "damage": (348, 18, 148, 76),
        }
        fields = {
            "student": ["STUDENT", "PK student_id", "name, email UQ", "student_number UQ"],
            "staff": ["STAFF", "PK staff_id", "name, email UQ", "role"],
            "category": ["CATEGORY", "PK category_id", "name UQ"],
            "equipment": ["EQUIPMENT", "PK equipment_id", "asset_tag UQ, name", "status", "FK category_id"],
            "loan": ["LOAN", "PK loan_id", "FK student_id", "FK staff_id", "FK equipment_id", "out_at, due_at, back_at"],
            "damage": ["DAMAGE_REPORT", "PK report_id", "FK loan_id, note", "created_at"],
        }

        def center(box):
            x, y, w, h = box
            return x + w / 2, y + h / 2

        def connect(a, b, label="", elbow=None):
            ax, ay = center(boxes[a])
            bx, by = center(boxes[b])
            c.setStrokeColor(LINE)
            c.setLineWidth(1.1)
            if elbow:
                c.line(ax, ay, elbow[0], ay)
                c.line(elbow[0], ay, elbow[0], by)
                c.line(elbow[0], by, bx, by)
            else:
                c.line(ax, ay, bx, by)
            if label:
                c.setFillColor(NAVY)
                c.setFont("Times-Italic", 7.5)
                c.drawCentredString((ax + bx) / 2, (ay + by) / 2 + 4, label)

        connect("student", "loan", "1 : N")
        connect("staff", "loan", "1 : N")
        connect("equipment", "loan", "1 : N")
        connect("category", "equipment", "1 : N")
        connect("loan", "damage", "1 : N")

        for key, (x, y, w, h) in boxes.items():
            rows = fields[key]
            c.setFillColor(FILL)
            c.setStrokeColor(NAVY)
            c.setLineWidth(1.2)
            c.roundRect(x, y, w, h, 4, fill=1, stroke=1)
            c.setFillColor(NAVY)
            c.rect(x, y + h - 16, w, 16, fill=1, stroke=0)
            c.setFillColor(white)
            c.setFont("Times-Bold", 8)
            c.drawCentredString(x + w / 2, y + h - 12, rows[0])
            c.setFillColor(black)
            c.setFont("Times-Roman", 7)
            yy = y + h - 28
            for line in rows[1:]:
                c.drawString(x + 8, yy, line)
                yy -= 11


def styles():
    ss = getSampleStyleSheet()
    ss.add(ParagraphStyle(
        "CoverSchool", fontName="Times-Bold", fontSize=11, textColor=NAVY,
        alignment=TA_CENTER, spaceAfter=2, leading=14,
    ))
    ss.add(ParagraphStyle(
        "CoverCourse", fontName="Times-Roman", fontSize=11, textColor=NAVY,
        alignment=TA_CENTER, spaceAfter=2, leading=14,
    ))
    ss.add(ParagraphStyle(
        "DocTitle", fontName="Times-Bold", fontSize=16, textColor=HEAD,
        alignment=TA_CENTER, spaceBefore=8, spaceAfter=4, leading=20,
    ))
    ss.add(ParagraphStyle(
        "DocSub", fontName="Times-Italic", fontSize=11, textColor=NAVY,
        alignment=TA_CENTER, spaceAfter=10, leading=14,
    ))
    ss.add(ParagraphStyle(
        "H1", fontName="Times-Bold", fontSize=13, textColor=NAVY,
        spaceBefore=12, spaceAfter=6, leading=16,
        borderPadding=2,
    ))
    ss.add(ParagraphStyle(
        "H2", fontName="Times-Bold", fontSize=11.5, textColor=HEAD,
        spaceBefore=8, spaceAfter=4, leading=14,
    ))
    ss.add(ParagraphStyle(
        "Body", fontName="Times-Roman", fontSize=11, alignment=TA_JUSTIFY,
        leading=14.5, spaceAfter=6, firstLineIndent=0,
    ))
    ss.add(ParagraphStyle(
        "BodyIndent", fontName="Times-Roman", fontSize=11, alignment=TA_JUSTIFY,
        leading=14.5, spaceAfter=6, firstLineIndent=12,
    ))
    ss.add(ParagraphStyle(
        "BulletBody", fontName="Times-Roman", fontSize=11, alignment=TA_LEFT,
        leading=14.5, leftIndent=18, bulletIndent=6, spaceAfter=3,
    ))
    ss.add(ParagraphStyle(
        "Caption", fontName="Times-Italic", fontSize=10, alignment=TA_CENTER,
        textColor=NAVY, spaceBefore=4, spaceAfter=10, leading=13,
    ))
    ss.add(ParagraphStyle(
        "Meta", fontName="Times-Roman", fontSize=11, alignment=TA_CENTER,
        spaceAfter=2, leading=14,
    ))
    ss.add(ParagraphStyle(
        "Cell", fontName="Times-Roman", fontSize=8.5, leading=11, alignment=TA_LEFT,
    ))
    ss.add(ParagraphStyle(
        "CellHead", fontName="Times-Bold", fontSize=8.5, leading=11,
        textColor=white, alignment=TA_CENTER,
    ))
    ss.add(ParagraphStyle(
        "Foot", fontName="Times-Roman", fontSize=8.5, textColor=HexColor("#555555"),
        alignment=TA_CENTER,
    ))
    return ss


def header_footer(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(NAVY)
    canvas.rect(0, letter[1] - 28, letter[0], 28, fill=1, stroke=0)
    canvas.setFillColor(white)
    canvas.setFont("Times-Roman", 8)
    canvas.drawString(54, letter[1] - 18, "University of Balamand  |  CSIS231 Assignment 1")
    canvas.drawRightString(letter[0] - 54, letter[1] - 18, "Project Proposal")
    canvas.setFillColor(NAVY)
    canvas.rect(0, 0, letter[0], 28, fill=1, stroke=0)
    canvas.setFillColor(white)
    canvas.setFont("Times-Roman", 8)
    canvas.drawString(54, 12, "LabLoan  |  Anthony Gerges, A2212825")
    canvas.drawRightString(letter[0] - 54, 12, f"Page {doc.page}")
    canvas.restoreState()


def p(ss, text, style="Body"):
    return Paragraph(text, ss[style])


def bullets(ss, items):
    out = []
    for item in items:
        out.append(Paragraph("•  " + item, ss["BulletBody"]))
    return out


def make_table(ss, headers, rows, col_widths):
    head = [Paragraph(h, ss["CellHead"]) for h in headers]
    data = [head]
    for row in rows:
        data.append([Paragraph(str(c), ss["Cell"]) for c in row])
    t = Table(data, colWidths=col_widths, repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), NAVY),
        ("TEXTCOLOR", (0, 0), (-1, 0), white),
        ("BACKGROUND", (0, 1), (-1, -1), LIGHT),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [LIGHT, white]),
        ("GRID", (0, 0), (-1, -1), 0.4, HexColor("#9AA4B2")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    return t


def build():
    ss = styles()
    doc = SimpleDocTemplate(
        OUT, pagesize=letter,
        leftMargin=0.75 * inch, rightMargin=0.75 * inch,
        topMargin=0.6 * inch, bottomMargin=0.55 * inch,
        title="LabLoan: CSIS231 Assignment 1 Project Proposal",
        author="Anthony Gerges",
    )
    story = []
    story.append(p(ss, "UNIVERSITY OF BALAMAND", "CoverSchool"))
    story.append(p(ss, "School of Arts and Sciences  ·  Computer Science", "CoverCourse"))
    story.append(p(ss, "CSIS231 — Assignment 1: Project Proposal", "CoverCourse"))
    story.append(p(ss, "LabLoan: Computer Science Laboratory Equipment Lending System", "DocTitle"))
    story.append(p(
        ss,
        "A JavaFX client–server desktop application with Spring Boot services and PostgreSQL storage",
        "DocSub",
    ))
    story.append(p(ss, "<b>Author:</b> Anthony Gerges &nbsp;&nbsp; <b>Student ID:</b> A2212825", "Meta"))
    story.append(p(ss, "23 September 2026", "Meta"))
    story.append(Spacer(1, 8))

    story.append(p(ss, "1. Idea and Expected Outcome", "H1"))
    story.append(p(ss, "1.1 Problem Statement", "H2"))
    story.append(p(ss,
        "The primary users are Computer Science students who borrow laboratory equipment "
        "(laptops, microcontroller kits, sensors, and similar assets) and laboratory assistants "
        "who must record each checkout and return. In many departmental labs this process still "
        "depends on paper logs or a shared spreadsheet. Those tools do not enforce a single source "
        "of truth: an item can be listed as available while it is still in a student’s bag, overdue "
        "loans are easy to miss, and damage is recorded, if at all, as unstructured notes. The pain "
        "point is operational rather than academic. Staff cannot answer, with confidence, which "
        "assets are free, who holds them, and when they are due back."
    ))
    story.append(p(ss, "1.2 Solution Overview", "H2"))
    story.append(p(ss,
        "LabLoan is a client–server desktop application. A JavaFX client presents catalog, checkout, "
        "return, overdue, and inventory screens. A Spring Boot server exposes JSON REST services for "
        "create, read, update, and delete operations. PostgreSQL stores students, staff, categories, "
        "equipment, loans, and damage reports. Maven builds the modules and Git versions the source. "
        "The design separates presentation (JavaFX), business rules (the service layer), and persistence "
        "(the relational schema), which matches the architectural focus of CSIS231."
    ))
    story.append(p(ss, "1.3 Success Criteria", "H2"))
    story.extend(bullets(ss, [
        "A classmate can sign in, register or select a student, add an equipment item, check it out, return it, and observe the same records after the application is restarted.",
        "An attempt to loan an item that is already on loan is rejected by the server with a conflict response, not only by the user interface.",
        "Overdue open loans can be listed without scanning the catalog by hand.",
        "All client–server exchanges use JSON over HTTP; the database is PostgreSQL.",
    ]))
    story.append(p(ss, "1.4 Scope Boundaries", "H2"))
    story.append(p(ss, "<b>In scope for this semester.</b> Authentication by role (student or staff); CRUD for students and equipment; loan creation and return; overdue listing; optional damage reports; JavaFX navigation among the views listed in Section 4; Maven and Git.", "Body"))
    story.append(p(ss, "<b>Out of scope.</b> Mobile clients, email or SMS reminders, barcode scanners, payment of fines, multi-campus deployments, three-dimensional or game clients, and any integration with other course projects. Fine amounts, if mentioned, remain a status flag rather than a payment workflow.", "Body"))

    story.append(p(ss, "2. Data Model and ERD", "H1"))
    story.append(p(ss, "2.1 Entity-Relationship Diagram", "H2"))
    story.append(p(ss,
        "Figure 1 shows the six entities and their identifying relationships. A student and a staff "
        "member participate in many loans. Each loan concerns one equipment item. Each equipment item "
        "belongs to one category. A loan may have zero or more damage reports. Cardinalities are one-to-many "
        "from parent to child; there is no many-to-many association that would require an extra bridge "
        "beyond LOAN itself, which already is the associative entity among student, staff, and equipment."
    ))
    story.append(Spacer(1, 4))
    story.append(ERDFigure(7.0 * inch, 3.15 * inch))
    story.append(p(ss, "Figure 1. LabLoan entity-relationship diagram (primary keys, foreign keys, and 1:N relationships).", "Caption"))

    story.append(p(ss, "2.2 Data Dictionary", "H2"))
    story.append(make_table(
        ss,
        ["Table", "Column", "Type", "Constraints"],
        [
            ["student", "student_id", "SERIAL / INT", "PRIMARY KEY"],
            ["student", "name", "VARCHAR(120)", "NOT NULL"],
            ["student", "email", "VARCHAR(180)", "NOT NULL, UNIQUE"],
            ["student", "student_number", "VARCHAR(32)", "NOT NULL, UNIQUE"],
            ["staff", "staff_id", "SERIAL / INT", "PRIMARY KEY"],
            ["staff", "name", "VARCHAR(120)", "NOT NULL"],
            ["staff", "email", "VARCHAR(180)", "NOT NULL, UNIQUE"],
            ["staff", "role", "VARCHAR(24)", "NOT NULL (ASSISTANT | ADMIN)"],
            ["category", "category_id", "SERIAL / INT", "PRIMARY KEY"],
            ["category", "name", "VARCHAR(80)", "NOT NULL, UNIQUE"],
            ["equipment", "equipment_id", "SERIAL / INT", "PRIMARY KEY"],
            ["equipment", "asset_tag", "VARCHAR(40)", "NOT NULL, UNIQUE"],
            ["equipment", "name", "VARCHAR(120)", "NOT NULL"],
            ["equipment", "status", "VARCHAR(16)", "NOT NULL CHECK (available | on_loan | repair)"],
            ["equipment", "category_id", "INT", "NOT NULL, FK → category"],
            ["loan", "loan_id", "SERIAL / INT", "PRIMARY KEY"],
            ["loan", "student_id", "INT", "NOT NULL, FK → student"],
            ["loan", "staff_id", "INT", "NOT NULL, FK → staff"],
            ["loan", "equipment_id", "INT", "NOT NULL, FK → equipment"],
            ["loan", "out_at", "TIMESTAMPTZ", "NOT NULL"],
            ["loan", "due_at", "TIMESTAMPTZ", "NOT NULL"],
            ["loan", "back_at", "TIMESTAMPTZ", "NULL until returned"],
            ["damage_report", "report_id", "SERIAL / INT", "PRIMARY KEY"],
            ["damage_report", "loan_id", "INT", "NOT NULL, FK → loan"],
            ["damage_report", "note", "TEXT", "NOT NULL"],
            ["damage_report", "created_at", "TIMESTAMPTZ", "NOT NULL"],
        ],
        [1.15 * inch, 1.35 * inch, 1.45 * inch, 2.85 * inch],
    ))
    story.append(p(ss, "Table 1. Data dictionary for LabLoan. Open loans are those with <font face='Times-Italic'>back_at</font> NULL. A partial unique index on <font face='Times-Italic'>equipment_id</font> WHERE <font face='Times-Italic'>back_at</font> IS NULL prevents two concurrent loans of the same asset.", "Caption"))

    story.append(p(ss, "2.3 Normalization Rationale", "H2"))
    story.append(p(ss,
        "The schema is designed in third normal form. Student and staff names are stored once and "
        "referenced by foreign key, so a name change does not rewrite loan history. Category names are "
        "not repeated on every equipment row. Loan facts (who, which item, when) are not mixed into the "
        "equipment table; equipment.status is a current-state attribute derived from the latest open loan "
        "and is maintained by the service layer in the same transaction as insert or update of LOAN. "
        "Damage notes depend on a loan, not on a repeating group inside EQUIPMENT. There are no "
        "transitive dependencies such as storing a student’s email on the loan row. Redundant copies of "
        "descriptive fields are therefore avoided while referential integrity remains enforceable in PostgreSQL."
    ))

    story.append(p(ss, "3. Service Layer (Server-Side)", "H1"))
    story.append(p(ss,
        "Services are Spring Boot REST controllers consuming and producing JSON. Identifiers in paths "
        "are integers. Bodies are resource representations. HTTP status codes form the success and error "
        "contract: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 404 Not Found, and 409 Conflict. "
        "Error bodies have the shape <font face='Courier'>{\"code\":\"CONFLICT\",\"message\":\"Equipment is already on loan.\"}</font>."
    ))

    story.append(p(ss, "3.1 Operations", "H2"))
    story.append(make_table(
        ss,
        ["Service", "Purpose and I/O", "Tables", "Validation"],
        [
            ["getEquipment()", "GET /api/equipment → 200 list of items with category name.", "equipment, category", "None beyond authentication."],
            ["saveEquipment(item)", "POST/PUT /api/equipment → 201/200 saved item.", "equipment", "asset_tag and name required; asset_tag unique; category_id must exist; status in the allowed set."],
            ["deleteEquipment(id)", "DELETE /api/equipment/{id} → 204.", "equipment", "404 if missing; 409 if an open loan references the item."],
            ["saveStudent(s) / getStudents()", "POST/GET /api/students → 201/200.", "student", "name, email, student_number required; email and student_number unique."],
            ["createLoan(dto)", "POST /api/loans {studentId, equipmentId, staffId, dueAt} → 201 loan.", "loan, equipment", "All FKs exist; equipment.status must be available; dueAt in the future; then insert loan and set status on_loan in one transaction."],
            ["returnLoan(id)", "POST /api/loans/{id}/return → 200 loan.", "loan, equipment", "Loan must exist and back_at must be null; set back_at = now(); set equipment.status available unless a new damage report forces repair."],
            ["getLoansByStudent(id)", "GET /api/students/{id}/loans → 200 list.", "loan, equipment", "404 if student does not exist."],
            ["getOverdueLoans()", "GET /api/loans/overdue → 200 list.", "loan, student, equipment", "Predicate: back_at IS NULL AND due_at &lt; now()."],
            ["saveDamageReport(dto)", "POST /api/loans/{id}/damage {note} → 201 report.", "damage_report, equipment", "note required and non-blank; loan must exist; optional transition of equipment.status to repair."],
        ],
        [1.35 * inch, 2.35 * inch, 1.15 * inch, 1.95 * inch],
    ))
    story.append(p(ss, "Table 2. Service contracts, CRUD mapping, and validation rules.", "Caption"))

    story.append(p(ss, "3.2 Error Handling", "H2"))
    story.append(p(ss,
        "Validation failures return 400 with a field-level message. Missing resources return 404. "
        "Business conflicts—duplicate asset tags, a second open loan on the same equipment, or deleting "
        "an item that is still borrowed—return 409. The JavaFX client does not invent these rules; it "
        "displays the server message in a dialog and leaves the form open so the user can correct input. "
        "Loan history is never physically deleted when an item comes back: the row is closed by setting "
        "back_at. That choice preserves an audit trail and keeps the model in third normal form."
    ))

    story.append(p(ss, "4. Client Views (JavaFX)", "H1"))
    story.append(p(ss, "4.1 Screens and View-to-Service Mapping", "H2"))
    story.append(p(ss,
        "The client follows a simple MVC structure suitable for later refinement toward MVVM. Each view "
        "is a JavaFX FXML scene. Controllers call the service layer through an HTTP client and bind JSON "
        "results to TableView and form controls. Table 3 lists every planned view, its purpose, and the "
        "services it consumes."
    ))
    story.append(make_table(
        ss,
        ["JavaFX view", "Purpose", "Services called"],
        [
            ["LoginView", "Identify student or staff and store session role and id.", "getStudents() or staff lookup (minimal for this semester)"],
            ["CatalogView", "Browse equipment, category, and current status.", "getEquipment()"],
            ["CheckoutView", "Create a loan at the desk (staff).", "getStudents(), getEquipment(), createLoan()"],
            ["MyLoansView", "Show the signed-in student’s current and past loans.", "getLoansByStudent(id)"],
            ["ReturnView", "Close an open loan (staff).", "getOverdueLoans() or loan search, returnLoan(id)"],
            ["OverdueView", "Staff report of late items.", "getOverdueLoans()"],
            ["InventoryView", "Add, update, or remove catalog items (staff).", "saveEquipment(), deleteEquipment(), getEquipment()"],
            ["DamageView", "Attach a damage note to a loan.", "saveDamageReport()"],
        ],
        [1.4 * inch, 2.5 * inch, 2.9 * inch],
    ))
    story.append(p(ss, "Table 3. JavaFX views and the server operations each view invokes.", "Caption"))

    story.append(p(ss, "4.2 Navigation, State, and UX Notes", "H2"))
    story.append(p(ss,
        "After a successful login the session holds user id and role. Students are routed to CatalogView "
        "and MyLoansView. Staff see those screens plus CheckoutView, ReturnView, OverdueView, InventoryView, "
        "and DamageView. A top bar provides navigation and a logout action that clears the session. "
        "Wireframes are intentionally simple: a table of records, a detail form, and primary buttons "
        "(Check out, Return, Save). Empty tables show a short placeholder. HTTP 400 and 409 responses "
        "open an alert with the server message; the user remains on the same view. Long operations show "
        "a progress indicator so the UI does not appear frozen. No local cache is treated as authoritative; "
        "after every successful write the relevant table is refreshed from GET."
    ))

    story.append(p(ss, "4.3 Client–Server Data Flow", "H2"))
    story.append(p(ss,
        "A representative checkout flow is: CheckoutView collects studentId, equipmentId, and dueAt → "
        "POST /api/loans with a JSON body → the service validates referential integrity and availability → "
        "PostgreSQL inserts LOAN and updates EQUIPMENT.status → 201 returns the loan JSON → the table "
        "refreshes via getEquipment(). The same pattern applies to returns and damage reports. This mapping "
        "is the required connection between JavaFX views and server-side services."
    ))

    story.append(p(ss, "5. Risks and Mitigations", "H1"))
    story.append(make_table(
        ss,
        ["Risk", "Mitigation"],
        [
            ["Double checkout of the same asset", "Partial unique index on open loans plus a 409 response; UI disables checkout when status is not available."],
            ["PostgreSQL not installed on a student PC", "Docker Compose file or a shared laboratory server; schema provided as SQL scripts in Git."],
            ["Too many screens for the calendar", "Minimum viable client is Login, Catalog, Checkout, and Return; overdue and damage follow."],
            ["JSON mismatch between client and server", "Shared DTO field names documented in this proposal; Maven modules compiled together."],
        ],
        [2.4 * inch, 5.4 * inch],
    ))
    story.append(p(ss, "Table 4. Principal risks and mitigations.", "Caption"))

    story.append(p(ss, "6. Milestones", "H1"))
    story.append(make_table(
        ss,
        ["Window", "Deliverable"],
        [
            ["Weeks 1–2", "Final ERD, PostgreSQL DDL, Git repository, Maven parent project."],
            ["Weeks 3–4", "Spring Boot CRUD for student, category, and equipment with JSON tests."],
            ["Weeks 5–6", "createLoan, returnLoan, transactional status updates, conflict handling."],
            ["Weeks 7–8", "JavaFX Login, Catalog, Checkout, and Return views wired to the APIs."],
            ["Week 9", "Overdue list and damage report; basic role-based menu."],
            ["Week 10", "End-to-end test, bug fixes, and demonstration."],
        ],
        [1.6 * inch, 6.2 * inch],
    ))
    story.append(p(ss, "Table 5. High-level schedule. Dates will follow the official CSIS231 calendar.", "Caption"))

    story.append(p(ss,
        "In summary, LabLoan is a feasible semester project: a bounded domain, a normalized six-table "
        "schema, an explicit JSON service contract, and a short set of JavaFX views that each call named "
        "server operations. The expected outcome is a restartable desktop demonstration in which laboratory "
        "equipment can be catalogued, lent, and returned through a well-architected client–server stack "
        "(JavaFX, Spring Boot, PostgreSQL, Maven, and Git)."
    ))

    doc.build(story, onFirstPage=header_footer, onLaterPages=header_footer)
    print("Wrote", OUT)


if __name__ == "__main__":
    build()
