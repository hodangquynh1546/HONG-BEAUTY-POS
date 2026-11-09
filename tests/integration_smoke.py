import json, os, secrets, subprocess, time, urllib.request, urllib.error
from pathlib import Path

root = Path(__file__).resolve().parents[1]
api = root / 'HongBeautyPOSAPI/src/HongBeautyPOS.Api'
env = dict(os.environ)
test_db = os.environ.get('POS_TEST_DATABASE_URL', '')
if not (test_db.startswith('Host=127.0.0.1;') or test_db.startswith('Host=localhost;')):
    raise SystemExit('POS_TEST_DATABASE_URL must point to a fresh localhost PostgreSQL database')
port = os.environ.get('POS_TEST_API_PORT', '5234')
env.update(DATABASE_URL=test_db,
           JWT_SECRET=secrets.token_urlsafe(48), OWNER_EMAIL='owner-' + secrets.token_hex(6) + '@example.invalid',
           OWNER_NAME='Test Owner', OWNER_PASSWORD='A9!' + secrets.token_urlsafe(24),
           ASPNETCORE_ENVIRONMENT='Development', ASPNETCORE_URLS='http://127.0.0.1:' + port)
migrate = subprocess.run([os.environ.get('DOTNET_COMMAND', 'dotnet'),'run','--project',str(api),'--no-build','--no-launch-profile','--','--migrate'],env=env,cwd=root,capture_output=True,text=True,timeout=40)
if migrate.returncode: raise RuntimeError('Migration failed: ' + migrate.stderr[-1200:])
bootstrap = subprocess.run([os.environ.get('DOTNET_COMMAND', 'dotnet'),'run','--project',str(api),'--no-build','--no-launch-profile','--','--bootstrap-owner'],env=env,cwd=root,capture_output=True,text=True,timeout=40)
if bootstrap.returncode: raise RuntimeError('Owner bootstrap failed: ' + bootstrap.stderr[-1200:])
log = open('/tmp/hbpos-integration-api.log','w')
server = subprocess.Popen([os.environ.get('DOTNET_COMMAND', 'dotnet'),'run','--project',str(api),'--no-build','--no-launch-profile'],env=env,cwd=root,stdout=log,stderr=subprocess.STDOUT)
base = 'http://127.0.0.1:' + port
token = None

def call(method,path,payload=None):
    headers = {'Content-Type':'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(base+path,data=data,headers=headers,method=method)
    try:
        with urllib.request.urlopen(req,timeout=10) as resp:
            return resp.status, json.loads(resp.read() or b'{}')
    except urllib.error.HTTPError as err:
        body = err.read().decode()
        raise RuntimeError(f'{method} {path}: HTTP {err.code} {body[:500]}')

try:
    for _ in range(40):
        try:
            urllib.request.urlopen(base+'/health',timeout=1)
            break
        except Exception: time.sleep(.25)
    else: raise RuntimeError('API not healthy: ' + Path('/tmp/hbpos-integration-api.log').read_text()[-1000:])
    _, auth = call('POST','/api/v1/auth/login',{'email':env['OWNER_EMAIL'],'password':env['OWNER_PASSWORD']})
    token = auth['accessToken']
    owner_token = token
    employee_email = 'employee-' + secrets.token_hex(6) + '@example.invalid'
    employee_password = 'B8!' + secrets.token_urlsafe(20)
    _, employee = call('POST','/api/v1/employees',{'email':employee_email,'fullName':'Test Employee','password':employee_password,'permissions':['products.read']})
    _, employee_auth = call('POST','/api/v1/auth/login',{'email':employee_email,'password':employee_password})
    token = employee_auth['accessToken']
    call('GET','/api/v1/products')
    try:
        call('POST','/api/v1/products',{'sku':'DENIED','name':'Denied','salePrice':1,'costPrice':1})
        raise AssertionError('Employee unexpectedly created a product')
    except RuntimeError as error:
        assert 'HTTP 403' in str(error), error
    token = owner_token
    call('PUT',f"/api/v1/employees/{employee['id']}/permissions",{'permissions':['products.read','reports.read']})
    token = employee_auth['accessToken']
    call('GET','/api/v1/reports/summary')
    try:
        call('PUT',f"/api/v1/employees/{employee['id']}/password",{'password':'Forbidden9!Password'})
        raise AssertionError('Employee changed their own password through Owner endpoint')
    except RuntimeError as error:
        assert 'HTTP 403' in str(error), error
    token = owner_token
    replacement_password = 'C7!' + secrets.token_urlsafe(20)
    call('PUT',f"/api/v1/employees/{employee['id']}/password",{'password':replacement_password})
    token = employee_auth['accessToken']
    try:
        call('GET','/api/v1/products')
        raise AssertionError('Old token survived employee password reset')
    except RuntimeError as error:
        assert 'HTTP 401' in str(error), error
    try:
        call('POST','/api/v1/auth/login',{'email':employee_email,'password':employee_password})
        raise AssertionError('Old employee password still works')
    except RuntimeError as error:
        assert 'HTTP 401' in str(error), error
    _, employee_auth = call('POST','/api/v1/auth/login',{'email':employee_email,'password':replacement_password})
    token = owner_token
    call('PATCH',f"/api/v1/employees/{employee['id']}/status",{'active':False})
    _, audit = call('GET','/api/v1/audit-events')
    assert {'EMPLOYEE_CREATED','EMPLOYEE_PERMISSIONS_REPLACED','EMPLOYEE_PASSWORD_RESET','EMPLOYEE_DEACTIVATED'}.issubset({event['action'] for event in audit})
    token = employee_auth['accessToken']
    try:
        call('GET','/api/v1/products')
        raise AssertionError('Deactivated employee unexpectedly accessed products')
    except RuntimeError as error:
        assert 'HTTP 401' in str(error), error
    token = owner_token
    _, category = call('POST','/api/v1/categories',{'name':'Cosmetics'})
    _, child = call('POST','/api/v1/categories',{'name':'Lipstick','parentId':category['id']})
    try:
        call('PUT',f"/api/v1/categories/{category['id']}",{'name':'Cosmetics','parentId':child['id']})
        raise AssertionError('Category cycle unexpectedly accepted')
    except RuntimeError as error:
        assert 'HTTP 400' in str(error), error
    try:
        call('PATCH',f"/api/v1/categories/{category['id']}/status",{'active':False})
        raise AssertionError('Parent category with active child unexpectedly deactivated')
    except RuntimeError as error:
        assert 'HTTP 409' in str(error), error
    _, renamed = call('PUT',f"/api/v1/categories/{child['id']}",{'name':'Lip colors','parentId':category['id']})
    assert renamed['name'] == 'Lip colors'
    _, off = call('PATCH',f"/api/v1/categories/{child['id']}/status",{'active':False})
    assert off['active'] is False
    _, on = call('PATCH',f"/api/v1/categories/{child['id']}/status",{'active':True})
    assert on['active'] is True
    barcode = '893' + str(secrets.randbelow(10**10)).zfill(10)
    _, product = call('POST','/api/v1/products',{'sku':'TEST-' + secrets.token_hex(4),'barcode':barcode,'name':'Test lipstick','unit':'thỏi','reorderLevel':3,'salePrice':100,'costPrice':50})
    assert product['barcode'] == barcode and product['unit'] == 'thỏi' and product['reorderLevel'] == 3
    _, barcode_results = call('GET',f'/api/v1/products?q={barcode}')
    assert len(barcode_results) == 1 and barcode_results[0]['id'] == product['id']
    pid = product['id']
    token = employee_auth['accessToken']
    try:
        call('PATCH',f'/api/v1/products/{pid}/status',{'active':False})
        raise AssertionError('Employee without products.write changed a product')
    except RuntimeError as error:
        assert 'HTTP 401' in str(error), error
    token = owner_token
    _, updated = call('PUT',f'/api/v1/products/{pid}',{'sku':product['sku'],'barcode':barcode,'name':'Updated lipstick','unit':'hộp','reorderLevel':5,'salePrice':100,'costPrice':50})
    assert updated['barcode'] == barcode and updated['unit'] == 'hộp' and updated['reorderLevel'] == 5
    assert updated['name'] == 'Updated lipstick'
    _, inactive = call('PATCH',f'/api/v1/products/{pid}/status',{'active':False})
    assert inactive['active'] is False
    _, active = call('PATCH',f'/api/v1/products/{pid}/status',{'active':True})
    assert active['active'] is True
    _, customer = call('POST','/api/v1/customers',{'fullName':'Test customer','phone':'0123456789'})
    _, customer_updated = call('PUT',f"/api/v1/customers/{customer['id']}",{'fullName':'Updated customer','phone':'0987654321'})
    assert customer_updated['fullName'] == 'Updated customer'
    _, customer_off = call('PATCH',f"/api/v1/customers/{customer['id']}/status",{'active':False})
    assert customer_off['active'] is False
    _, customer_on = call('PATCH',f"/api/v1/customers/{customer['id']}/status",{'active':True})
    assert customer_on['active'] is True
    _, supplier = call('POST','/api/v1/suppliers',{'code':'SUP-' + secrets.token_hex(4),'name':'Test supplier'})
    _, supplier_updated = call('PUT',f"/api/v1/suppliers/{supplier['id']}",{'code':supplier['code'],'name':'Updated supplier'})
    assert supplier_updated['name'] == 'Updated supplier'
    _, supplier_off = call('PATCH',f"/api/v1/suppliers/{supplier['id']}/status",{'active':False})
    assert supplier_off['active'] is False
    _, supplier_on = call('PATCH',f"/api/v1/suppliers/{supplier['id']}/status",{'active':True})
    assert supplier_on['active'] is True
    _, purchase = call('POST','/api/v1/purchases',{'supplierId':supplier['id'],'items':[{'productId':pid,'quantity':10,'unitCost':50}]})
    call('POST',f"/api/v1/purchases/{purchase['id']}/complete",{})
    _, stock = call('GET','/api/v1/stock')
    assert next(x for x in stock if x['productId']==pid)['quantity']==10
    _, sale = call('POST','/api/v1/orders',{'idempotencyKey':str(__import__('uuid').uuid4()),'discount':0,'items':[{'productId':pid,'quantity':2}]})
    order_id=sale['id']
    call('POST',f'/api/v1/orders/{order_id}/payments',{'idempotencyKey':str(__import__('uuid').uuid4()),'method':'Cash','amount':200})
    _, receipt = call('GET',f'/api/v1/orders/{order_id}/receipt')
    assert receipt['orderNo'] == sale['orderNo'] and receipt['total'] == 200 and receipt['paidTotal'] == 200
    assert receipt['remainingDebt'] == 0 and len(receipt['items']) == 1 and receipt['items'][0]['lineTotal'] == 200
    assert len(receipt['payments']) == 1 and receipt['payments'][0]['amount'] == 200
    _, lines=call('GET',f'/api/v1/orders/{order_id}/items')
    _, ret=call('POST',f'/api/v1/orders/{order_id}/returns',{'idempotencyKey':str(__import__('uuid').uuid4()),'reason':'Test customer return','refundAmount':100,'refundMethod':'Cash','items':[{'orderItemId':lines[0]['id'],'quantity':1,'restock':True}]})
    assert ret['refundAmount']==100
    _, audit = call('GET','/api/v1/audit-events')
    assert any(event['action']=='SALE_RETURN_CREATED' and event['subjectId']==ret['id'] for event in audit)
    _, stock=call('GET','/api/v1/stock')
    assert next(x for x in stock if x['productId']==pid)['quantity']==9
    _, take=call('POST','/api/v1/stocktakes',{})
    item=next(x for x in take['items'] if x['productId']==pid)
    call('PUT',f"/api/v1/stocktakes/{take['id']}/items/{item['id']}",{'actualQuantity':8})
    call('POST',f"/api/v1/stocktakes/{take['id']}/complete",{})
    _, stock=call('GET','/api/v1/stock')
    assert next(x for x in stock if x['productId']==pid)['quantity']==8
    _, report=call('GET','/api/v1/reports/summary')
    assert report['grossSales']==200, report
    assert report['returnedSales']==100, report
    assert report['netRevenue']==100, report
    assert report['cashReceived']==200, report
    assert report['cashRefunded']==100, report
    assert report['netCashFlow']==100, report
    assert report['costOfGoods']==50, report
    print('PASS: report balances')
    print('PASS: employee permission and deactivation checks')
    print('PASS: migrate, bootstrap, login, product, supplier, purchase, sale, payment, return, stocktake')
finally:
    server.terminate()
    try: server.wait(timeout=5)
    except subprocess.TimeoutExpired: server.kill()
    log.close()
