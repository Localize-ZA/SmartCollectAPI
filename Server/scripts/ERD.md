Government

government_id (PK)

name

type (national, provincial, local)

official_data (laws, regulations, announcements)

Business

business_id (PK)

registration_no

name

sector (JSE-listed? manufacturing? etc.)

financials (revenue, debt, tax paid, etc.)

supply_chain_id (FK → SupplyChain)

User

user_id (PK)

registration_no

name

role (citizen, business_rep, government_official)

contact_info

SupplyChain

supply_chain_id (PK)

business_id (FK → Business)

inputs (raw material sources, products, services)

outputs (finished goods, export categories)

status (active, disrupted, bankrupt)

Transaction

transaction_id (PK)

business_id (FK → Business)

government_id (FK → Government, for regulation/taxation)

product_id (FK → Product)

payment_id

timestamp

iso_8583_msg / iso_20022_msg

map_data (lat, long)

Product

product_id (PK)

name

type (commodity, manufactured good, service)

unit_price

Complaint / Message

complaint_id (PK)

user_id (FK → User)

business_id (FK → Business, optional)

government_id (FK → Government, optional)

content (text, media, metadata)

status (open, reviewed, resolved)

timestamp

EconomySnapshot

snapshot_id (PK)

country_id

gdp

debt

exports_total

imports_total

bankruptcies_total

timestamp

LLMExport

export_id (PK)

business_id (FK → Business, optional)

government_id (FK → Government, optional)

data_reference (transaction, supply_chain, complaints, etc.)

prediction_result (success rate, failure probability)

timestamp

Relationships

A Government oversees many Businesses, many SupplyChains, and collects Transactions.

A Business has many SupplyChains, many Transactions, and may receive many Complaints.

A User can send many Complaints to Businesses or Government.

A SupplyChain contains many Products and links across businesses (suppliers/customers).

An EconomySnapshot aggregates data from Transactions, SupplyChains, Business financials, and Government reports.

LLMExport pulls from all entities to generate predictions.

ERD (Textual Sketch)
Government (government_id PK) ───────< Transaction >─────── Business (business_id PK)
        │                                        │
        │                                        │
        │                                        │
        └────────< Complaint >──────── User (user_id PK) ──────> Business
        │
        │
        └────────< EconomySnapshot >──────── Country

Business ───────< SupplyChain (supply_chain_id PK) >────── Product (product_id PK)

LLMExport (export_id PK)
    ↳ references Business, Government, Transactions, SupplyChains, Complaints
